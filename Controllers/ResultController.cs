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
        .OrderByDescending(t => t.Time)
        .Take(4)
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

    [HttpGet("latestAssessment")]
    public async Task<ActionResult> GetLatestAssessment([FromQuery] int userId)
    {
        var latest = await _context.SelfAssessment
            .Where(u => u.UserId == userId)
            .OrderByDescending(t => t.Time)
            .Select(t => new
            {
                t.Id,
                time = t.Time.ToString("dd-MM-yyyy"),
                level = t.Level
            })
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return Ok(null);
        }

        var scores = await _context.AssessmentAnswer
            .Where(aa => aa.SelfAssessmentId == latest.Id)
            .Join(
                _context.BctqAnswers,
                aa => aa.BctqAnswerId,
                ba => ba.Id,
                (aa, ba) => new { aa, ba }
            )
            .Join(
                _context.BctqQuestions,
                x => x.ba.BctqQuestionId,
                bq => bq.Id,
                (x, bq) => new { x.ba, bq }
            )
            .GroupBy(x => 1)
            .Select(g => new
            {
                symptomScore = g
                    .Where(x => x.bq.QuestionTypeId == 1)
                    .Sum(x => x.ba.Rate),

                functionScore = g
                    .Where(x => x.bq.QuestionTypeId == 2)
                    .Sum(x => x.ba.Rate)
            })
            .FirstOrDefaultAsync();

        var physicalResults = await _context.AssessmentPhysicalDetail
            .Where(pd => pd.SelfAssessmentId == latest.Id)
            .Join(
                _context.PhysicalTests,
                pd => pd.PhysicalTestId,
                pt => pt.Id,
                (pd, pt) => new
                {
                    name = pt.Name
                    .Replace("Nghiệm pháp ", "")
                    .Replace("Nghiệm pháp", "")
                    .Trim(),
                    isPositive = pd.IsPositive
                }
            )
            .ToListAsync();

        var data = new
        {
            time = latest.time,
            level = latest.level,
            symptomScore = scores?.symptomScore ?? 0,
            functionScore = scores?.functionScore ?? 0,
            physicalResults = physicalResults
        };

        return Ok(data);
    }

    [HttpGet("assessments")]
    public async Task<ActionResult> GetAllAssessments([FromQuery] int userId)
    {
        var latest = await _context.SelfAssessment
        .Where(u => u.UserId == userId)
        .OrderBy(t => t.Time)
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