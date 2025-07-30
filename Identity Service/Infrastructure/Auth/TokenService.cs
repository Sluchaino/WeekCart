using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Auth;

public sealed class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly ILogger<TokenService> _logger;

    public TokenService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IOptions<JwtOptions> jwtOptions,
        ILogger<TokenService> logger)
    {
        _userManager = userManager;
        _db = db;
        _jwt = jwtOptions.Value;
        _logger = logger;

        // Validate JWT configuration
        if (string.IsNullOrEmpty(_jwt.Key) || _jwt.Key.Length < 32)
            throw new ArgumentException("JWT key must be at least 32 characters long");
    }

    public async Task<(string accessToken, string refreshToken)> IssueTokensAsync(ApplicationUser user, CancellationToken ct = default)
    {
        try
        {
            string accessToken = await CreateJwtAsync(user);

            var refresh = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Token = GenerateSecureString(),
                Expires = DateTime.UtcNow.AddDays(_jwt.RefreshDays),
                Revoked = false
            };

            _db.RefreshTokens.Add(refresh);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Tokens issued for user {UserId}", user.Id);

            return (accessToken, refresh.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to issue tokens for user {UserId}", user.Id);
            throw;
        }
    }

    public async Task<string> RotateRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens
                             .FirstOrDefaultAsync(x => x.Token == refreshToken, ct);
        if (token is null)
        {
            _logger.LogWarning("Refresh token not found: {Token}", refreshToken);
            throw new SecurityTokenException("Refresh token is invalid");
        }

        if (token.Revoked || token.ReplacedBy != null)
        {
            _logger.LogWarning("Refresh token has been revoked: {Token}", refreshToken);
            throw new SecurityTokenException("Refresh token has been revoked");
        }

        if (token.Expires < DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token expired: {Token}", refreshToken);
            throw new SecurityTokenException("Refresh token expired");
        }

        token.Revoked = true;

        var user = await _userManager.FindByIdAsync(token.UserId.ToString());
        if (user is null)
        {
            _logger.LogWarning("User not found for token rotation: {UserId}", token.UserId);
            throw new SecurityTokenException("User not found");
        }

        var newAccess = await CreateJwtAsync(user);
        var newRefresh = await CreateRefreshAsync(user, ct);

        token.ReplacedBy = newRefresh;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Token rotated for user {UserId}", user.Id);

        return newAccess;
    }

    public async Task RevokeUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
                              .Where(t => t.UserId == userId && !t.Revoked)
                              .ToListAsync(ct);

        foreach (var t in tokens)
            t.Revoked = true;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Refresh tokens revoked for user {UserId}", userId);
    }

    private async Task<string> CreateJwtAsync(ApplicationUser user)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
        new Claim(JwtRegisteredClaimNames.Email, user.Email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        new Claim(JwtRegisteredClaimNames.Iat,
                  DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                  ClaimValueTypes.Integer64)
    };

        foreach (var role in await _userManager.GetRolesAsync(user))
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessMinutes),
            signingCredentials: creds);

        return _handler.WriteToken(token);
    }

    private async Task<string> CreateRefreshAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = GenerateSecureString(),
            Expires = DateTime.UtcNow.AddDays(_jwt.RefreshDays),
            Revoked = false
        };

        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);

        return token.Token;
    }

    private static string GenerateSecureString()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }
}