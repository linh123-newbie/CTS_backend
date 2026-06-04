using CTS_backend.Data;
using CTS_backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuestionController : ControllerBase
{
    private readonly AppDbContext _context;

    public QuestionController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("symptom")]
    public async Task<ActionResult> GetSymptomQuestionAnswer()
    {
        var data = await _context.BctqQuestions
            .AsNoTracking()
            .Where(q=>q.QuestionTypeId==1)
            .Select(q => new
            {
                q.Id,
                q.Content,
                q.QuestionTypeId,
                Answers = q.Answers.Select(a => new
                {
                    a.Id,
                    a.AnswerContent,
                    a.Rate
                }).ToList()
            })
            .ToListAsync();

        return Ok(data);
    }
    [HttpGet("function")]
    public async Task<ActionResult> GetFunctionQuestionAnswer()
    {
        var data = await _context.BctqQuestions
            .AsNoTracking()
            .Where(q=>q.QuestionTypeId==2)
            .Select(q => new
            {
                q.Id,
                q.Content,
                q.QuestionTypeId,
                Answers = q.Answers.Select(a => new
                {
                    a.Id,
                    a.AnswerContent,
                    a.Rate
                }).ToList()
            })
            .ToListAsync();

        return Ok(data);
    }
    [HttpGet("phalen")]
    public async Task<ActionResult> GetPhalenQuestion()
    {
        var data = await _context.PhysicalTests
            .AsNoTracking()
            .Select(q => new
            {
                q.Id,
                q.Name,
                q.ImageUrl,
                q.Duration,
                q.Description,
                
            })
            .ToListAsync();

        return Ok(data);
    }

}