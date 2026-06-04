using CTS_backend.Data;
using CTS_backend.Models.DTOs;
using CTS_backend.Models;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new
            {
                success = false,
                message = "IdToken is required"
            });
        }

        try
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];

            if (string.IsNullOrWhiteSpace(googleClientId))
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Google ClientId is not configured"
                });
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { googleClientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);

            if (!payload.EmailVerified)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Google email is not verified"
                });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(x => x.GoogleId == payload.Subject);

            if (user == null)
            {
                user = new Users
                {
                    GoogleId = payload.Subject,
                    Name = payload.Name ?? "",
                };

                _context.Users.Add(user);
            }
            else
            {
                user.GoogleId = payload.Subject;
                user.Name = payload.Name ?? user.Name;
            }

            await _context.SaveChangesAsync();

            return Ok(user);
        }
        catch
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid Google token"
            });
        }
    }
}