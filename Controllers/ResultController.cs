using CTS_backend.Data;
using CTS_backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResultController : ControllerBase
{
    private readonly AppDbContext _context;

    public ResultController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("latest")]
    public async Task<ActionResult> GetLatestSelfAssessment([FromQuery] int userId)
    {
        var latest = await _context.SelfAssessment
        .Where(u => u.UserId == userId)
        .OrderByDescending(t => t.Time)
        .Select(t => new
        {
            time = t.Time.ToString("dd-MM-yyyy"),
            level = t.Level,
        })
        .FirstOrDefaultAsync();

        if (latest == null)
        {
            return Ok(null);
        }

        return Ok(latest);
    }

    [HttpGet("latestAssessments")]
    public async Task<ActionResult> GetLatestSelfAssessments([FromQuery] int userId)
    {
        var latest = await _context.SelfAssessment
        .Where(u => u.UserId == userId)
        .Select(t => new
        {
            time = t.Time.ToString("dd-MM"),
            score = t.Score,
            level = t.Level
        })
        .ToListAsync();

        if (latest == null)
        {
            return Ok(null);
        }

        return Ok(latest);
    }

}