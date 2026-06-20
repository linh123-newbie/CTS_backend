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
            _logger.LogInformation("Backend Google ClientId={ClientId}", googleClientId);

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
                    Email = payload.Email,
                    RoleId = 1
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                user.GoogleId = payload.Subject;
                user.Name = payload.Name ?? user.Name;
            }

            await _context.SaveChangesAsync();

            var staff = await _context.Staffs
    .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (staff == null)
            {
                _logger.LogInformation("Creating staff for doctor user. UserId={UserId}", user.Id);

                staff = new Staffs
                {
                    UserId = user.Id,
                    Name = user.Name
                };

                _context.Staffs.Add(staff);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("GoogleLoginDoctor success. UserId={UserId}", user.Id);


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

        if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
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
    .FirstOrDefaultAsync(x =>
        x.GoogleId == payload.Subject ||
        x.Email == payload.Email
    );

            if (user == null)
            {
                user = new Users
                {
                    GoogleId = payload.Subject,
                    Name = payload.Name ?? "",
                    Email = payload.Email,
                    RoleId = 1
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {

                user.GoogleId = payload.Subject;
                user.Name = payload.Name ?? user.Name;
                user.Email = payload.Email ?? user.Email;
                user.RoleId = 1;
            }

            await _context.SaveChangesAsync();
            var staff = await _context.Staffs
            .FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (staff == null)
            {
                staff = new Staffs
                {
                    UserId = user.Id,
                    Name = user.Name
                };

                _context.Staffs.Add(staff);
                await _context.SaveChangesAsync();
            }

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