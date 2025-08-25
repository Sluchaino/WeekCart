using Application.DTO;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Identity_Service.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private const string UserRole =  "USER";
    private const string AdminRole = "ADMIN";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    //  REGISTER
    // -------------------------------------------------------------------------
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
    {
        _logger.LogInformation("Registration attempt for: {Email}", dto.Email);

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Registration failed for {Email}: {Errors}",
                dto.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            return BadRequest(result.Errors.Select(e => e.Description));
        }

        await _userManager.AddToRoleAsync(user, UserRole); // <-- единственный источник прав

        var tokens = await _tokenService.IssueTokensAsync(user);

        _logger.LogInformation("User {UserId} registered successfully", user.Id);

        return CreatedAtAction(nameof(Register), new
        {
            accessToken = tokens.accessToken,
            refreshToken = tokens.refreshToken
        });
    }

    // -------------------------------------------------------------------------
    //  LOGIN
    // -------------------------------------------------------------------------
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        _logger.LogInformation("Login attempt for: {Email}", dto.Email);

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
        {
            _logger.LogWarning("User not found: {Email}", dto.Email);
            return Unauthorized("Неверная почта или пароль");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Account locked: {Email}", dto.Email);
            return Unauthorized("Аккаунт временно заблокирован");
        }

        if (!await _userManager.CheckPasswordAsync(user, dto.Password))
        {
            _logger.LogWarning("Invalid password for: {Email}", dto.Email);
            await _userManager.AccessFailedAsync(user);
            return Unauthorized("Неверная почта или пароль");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var tokens = await _tokenService.IssueTokensAsync(user);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return Ok(new
        {
            accessToken = tokens.accessToken,
            refreshToken = tokens.refreshToken
        });
    }

    // -------------------------------------------------------------------------
    //  SELF-DELETE
    // -------------------------------------------------------------------------
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe([FromBody] DeleteAccountDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (!await _userManager.CheckPasswordAsync(user, dto.PasswordConfirmation))
            return BadRequest("Invalid password");

        await SoftDeleteUserAsync(user);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    //  ADMIN DELETE
    // -------------------------------------------------------------------------
    [Authorize(Roles = AdminRole)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            _logger.LogWarning("Admin tried to delete non-existent user: {UserId}", id);
            return NotFound();
        }

        await SoftDeleteUserAsync(user);

        _logger.LogInformation("Admin deleted user {UserId}", user.Id);
        return NoContent();
    }

    // -------------------------------------------------------------------------
    //  REFRESH
    // -------------------------------------------------------------------------
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
            return BadRequest("Refresh-токен отсутствует");

        try
        {
            var newAccess = await _tokenService.RotateRefreshAsync(dto.RefreshToken);
            return Ok(new { accessToken = newAccess });
        }
        catch (SecurityTokenException ex)
        {
            return Unauthorized(ex.Message);
        }
    }

    // -------------------------------------------------------------------------
    //  TEST
    // -------------------------------------------------------------------------
    [HttpGet("test")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult TestAuth()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Ok(new { sub });
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("identity")]
    public IActionResult GetIdentity()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = User.FindFirstValue(ClaimTypes.Email);

        return Ok(new
        {
            userId,
            email,
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
    // -------------------------------------------------------------------------
    //  HELPERS
    // -------------------------------------------------------------------------
    private async Task SoftDeleteUserAsync(ApplicationUser user)
    {
        await _tokenService.RevokeUserRefreshTokensAsync(user.Id);
        await _userManager.UpdateSecurityStampAsync(user);

        user.IsDeleted = true;
        user.DeletedAtUtc = DateTime.UtcNow;

        var archived = $"{user.Email}.deleted.{Guid.NewGuid():N}";
        user.Email = archived;
        user.NormalizedEmail = archived.ToUpperInvariant();
        user.UserName = archived;
        user.NormalizedUserName = archived;

        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        await _userManager.UpdateAsync(user);
    }
}