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

        public AuthController(UserManager<ApplicationUser> userManager,
                              SignInManager<ApplicationUser> signInManager,
                              ITokenService tokenService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
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

            if (!signInRes.Succeeded)
                return Unauthorized("Неверная почта или пароль");

            var tokens = await _tokenService.IssueTokensAsync(user);
            return Ok(new
            {
                accessToken = tokens.accessToken,
                refreshToken = tokens.refreshToken
            });
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
    }
}
