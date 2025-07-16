using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Application.DTO;

namespace Identity_Service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ITokenService tokenService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _logger = logger;
        }
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            var user = new ApplicationUser
            {
                UserName = dto.Email,
                Email = dto.Email,
                DisplayName = dto.DisplayName
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // при необходимости отправьте письмо с подтверждением e-mail здесь

            var tokens = await _tokenService.IssueTokensAsync(user);
            return CreatedAtAction(nameof(Register), new
            {
                accessToken = tokens.accessToken,
                refreshToken = tokens.refreshToken
            });
        }
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDTO dto)
        {
            var user = await _userManager.FindByEmailAsync(dto.Email);
            if (user is null)
                return Unauthorized("Неверная почта или пароль");

            var signInRes = await _signInManager.CheckPasswordSignInAsync(
                                user, dto.Password, lockoutOnFailure: false);

            //if (!signInRes.Succeeded)
            //    return Unauthorized("Неверная почта или пароль");

            var tokens = await _tokenService.IssueTokensAsync(user);
            return Ok(new
            {
                accessToken = tokens.accessToken,
                refreshToken = tokens.refreshToken
            });
        }
        [Authorize]
        [HttpDelete("me")]
        public async Task<IActionResult> DeleteMe([FromBody] DeleteAccountDto dto,
                                              CancellationToken ct)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user is null) return NotFound();

            // Проверяем пароль (если требуется подтверждение)
            var pwdOk = await _signInManager.CheckPasswordSignInAsync(user, dto.PasswordConfirmation, false);
            if (!pwdOk.Succeeded)
                return BadRequest("Неверный пароль.");

            await SoftDeleteUserAsync(user, ct);

            _logger.LogInformation("User {UserId} self-deleted.", user.Id);
            return NoContent(); // 204
        }
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user is null) return NotFound();

            await SoftDeleteUserAsync(user, ct);

            _logger.LogInformation("Admin deleted user {UserId}.", user.Id);
            return NoContent();
        }
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

        private async Task SoftDeleteUserAsync(ApplicationUser user, CancellationToken ct)
        {
            // revoke refresh tokens
            await _tokenService.RevokeUserRefreshTokensAsync(user.Id, ct);

            // update security stamp (инвалидирует аутентификацию)
            await _userManager.UpdateSecurityStampAsync(user);

            // soft delete flags
            user.IsDeleted = true;
            user.DeletedAtUtc = DateTime.UtcNow;

            // (опц.) освобождаем email
            var archivedEmail = $"{user.Email}.deleted.{Guid.NewGuid():N}";
            user.Email = archivedEmail;
            user.NormalizedEmail = archivedEmail.ToUpperInvariant();
            user.UserName = archivedEmail;
            user.NormalizedUserName = archivedEmail;

            // блокируем логин
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            await _userManager.UpdateAsync(user);

            // (опц.) излучаем событие в Kafka
            // await _eventBus.PublishAsync(new UserDeleted(user.Id, user.DeletedAtUtc.Value), ct);
        }
    }
}
