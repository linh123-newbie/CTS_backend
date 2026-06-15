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
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, IConfiguration configuration, ILogger<AuthController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;

    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        _logger.LogInformation("Google login API called");

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            _logger.LogWarning("IdToken is empty");

            return BadRequest(new
            {
                success = false,
                message = "IdToken is required"
            });
        }

        try
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];

            _logger.LogInformation("Backend Google ClientId: {ClientId}", googleClientId);

            if (string.IsNullOrWhiteSpace(googleClientId))
            {
                _logger.LogError("Google ClientId is not configured");

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

            _logger.LogInformation("Google token validated. Email: {Email}, Name: {Name}", payload.Email, payload.Name);

            if (!payload.EmailVerified)
            {
                _logger.LogWarning("Google email is not verified: {Email}", payload.Email);

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
        catch (Exception ex)
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];

            _logger.LogError(ex, "Google login failed. Backend ClientId: {ClientId}", googleClientId);

            return Unauthorized(new
            {
                success = false,
                message = "Invalid Google token",
                detail = ex.Message,
                backendClientId = googleClientId
            });
        }
    }
}