using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Auth;

/// <inheritdoc />
public sealed class TokenService : ITokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _jwt;
    private readonly JwtSecurityTokenHandler _handler = new();

    public TokenService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _db = db;
        _jwt = jwtOptions.Value;
    }

    /*-------------------------------------------------------------
     * PUBLIC API
     *-----------------------------------------------------------*/

    public async Task<(string accessToken, string refreshToken)> IssueTokensAsync(ApplicationUser user,
                                                                                 CancellationToken ct = default)
    {
        string access = CreateJwt(user);
        string refresh = await CreateRefreshAsync(user, ct);
        return (access, refresh);
    }

    public async Task<string> RotateRefreshAsync(string refreshToken,
                                                 CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens
                             .FirstOrDefaultAsync(x => x.Token == refreshToken, ct);
        if (token is null || token.Revoked || token.Expires < DateTime.UtcNow)
            throw new SecurityTokenException("Refresh token is invalid");

        token.Revoked = true;

        var user = await _userManager.FindByIdAsync(token.UserId.ToString())
                   ?? throw new SecurityTokenException("User not found");

        var newAccess = CreateJwt(user);
        var newRefresh = await CreateRefreshAsync(user, ct);

        token.ReplacedBy = newRefresh;
        await _db.SaveChangesAsync(ct);

        return newAccess;
    }

    public async Task RevokeUserRefreshTokensAsync(Guid userId,
                                                   CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
                              .Where(t => t.UserId == userId && !t.Revoked)
                              .ToListAsync(ct);

        foreach (var t in tokens)
            t.Revoked = true;

        await _db.SaveChangesAsync(ct);
    }

    /*-------------------------------------------------------------
     * PRIVATE HELPERS
     *-----------------------------------------------------------*/

    private string CreateJwt(ApplicationUser user)
    {
        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email,          user.Email!)
        };

        // роли добавим для примера
        var roles = _userManager.GetRolesAsync(user).Result;
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessMinutes),
            signingCredentials: creds);

        return _handler.WriteToken(token);
    }

    private async Task<string> CreateRefreshAsync(ApplicationUser user,
                                                  CancellationToken ct)
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
        // 32-байтный случайный токен &rarr; Base64Url
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }
}