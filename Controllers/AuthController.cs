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

    [HttpPost("google-login_doctor")]
    public async Task<IActionResult> GoogleLoginDoctor([FromBody] GoogleLoginRequest request)
    {
        _logger.LogInformation("GoogleLoginDoctor called");

        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            _logger.LogWarning("GoogleLoginDoctor failed: IdToken is empty");

            return BadRequest(new
            {
                success = false,
                message = "IdToken is required"
            });
        }

        try
        {
            var googleClientId = _configuration["Authentication:Google:ClientId"];

            _logger.LogInformation("Google ClientId configured: {HasClientId}", !string.IsNullOrWhiteSpace(googleClientId));

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

            _logger.LogInformation(
                "Google token valid. Email={Email}, Subject={Subject}, Verified={Verified}",
                payload.Email,
                payload.Subject,
                payload.EmailVerified
            );

            if (!payload.EmailVerified)
            {
                _logger.LogWarning("Google email not verified: {Email}", payload.Email);

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
                _logger.LogInformation("Creating new doctor user: {Email}", payload.Email);

                user = new Users
                {
                    GoogleId = payload.Subject,
                    Name = payload.Name ?? "",
                    RoleId = 2
                };

                _context.Users.Add(user);
            }
            else
            {
                _logger.LogInformation("Updating existing doctor user. UserId={UserId}", user.Id);

                user.GoogleId = payload.Subject;
                user.Name = payload.Name ?? user.Name;
                user.RoleId = 2;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("GoogleLoginDoctor success. UserId={UserId}", user.Id);

            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GoogleLoginDoctor failed");

            return Unauthorized(new
            {
                success = false,
                message = "Invalid Google token"
            });
        }
    }
}