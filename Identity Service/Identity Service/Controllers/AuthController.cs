using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Identity_Service.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _um;
        private readonly SignInManager<ApplicationUser> _sm;
        private readonly ITokenService _ts;

        [HttpPost("register")]
        public async Task<IActionResult> Register()
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                //var req = await _userService.Add(registerForm.FirstName, registerForm.SecondName, registerForm.NumberPhone
                    //, registerForm.Gender, registerForm.GetMailing, registerForm.Email, registerForm.Password);
                return Ok(new
                {
                    //UserId = req
                });
            }
            catch (Exception e)
            {
                return Conflict(e.Message);

            }
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login()
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                //var req = await _userService.Get(loginForm.Email, loginForm.Password);
                return Ok(new
                {
                    //UserId = req
                });
            }
            catch (Exception e)
            {
                return Conflict(e.Message);
            }
        }
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            try
            {
                //var req = await _userService.Get(loginForm.Email, loginForm.Password);
                return Ok(new
                {
                    //UserId = req
                });
            }
            catch (Exception e)
            {
                return Conflict(e.Message);
            }
        }
    }
}
