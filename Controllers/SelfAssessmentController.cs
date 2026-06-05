using CTS_backend.Data;
using CTS_backend.DTOs;
using CTS_backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SelfAssessmentController : ControllerBase
{
    private readonly AppDbContext _context;

    public SelfAssessmentController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("create")]
    public async Task<ActionResult> CreateSelfAssessment(
    [FromQuery] int userId,
    [FromBody] CreateSelfAssessmentRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);

            if (!userExists)
            {
                return NotFound(new
                {
                    message = "User not found",
                    userId = userId
                });
            }

            const int symptomTypeId = 1;
            const int functionTypeId = 2;
            String level = "";

            var selectedAnswers = await _context.BctqAnswers
                .Include(a => a.BctqQuestion)
                .Where(a => request.BctqAnswerIds.Contains(a.Id))
                .ToListAsync();

            decimal symptomScore = selectedAnswers
                .Where(a => a.BctqQuestion.QuestionTypeId == symptomTypeId)
                .Sum(a => a.Rate);

            decimal functionScore = selectedAnswers
                .Where(a => a.BctqQuestion.QuestionTypeId == functionTypeId)
                .Sum(a => a.Rate);

            var physicalTestIds = request.PhysicalDetails
                .Select(p => p.PhysicalTestId)
                .Distinct()
                .ToList();

            var physicalTests = await _context.PhysicalTests
                .Where(p => physicalTestIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            var physicalResults = request.PhysicalDetails.Select(item =>
{
    var testName = physicalTests.ContainsKey(item.PhysicalTestId)
        ? physicalTests[item.PhysicalTestId].Name
        : "Không xác định";

    var score = item.IsPositive ? 0 : 5;

    return new
    {
        physicalTestId = item.PhysicalTestId,
        name = testName,
        isPositive = item.IsPositive,
    };
}).ToList();

            decimal totalScore = symptomScore + functionScore;

            var selfAssessment = new SelfAssessment
            {
                UserId = userId,
                Time = DateTime.UtcNow,
                Score = totalScore,
                Level = null,
            };

            _context.SelfAssessment.Add(selfAssessment);
            await _context.SaveChangesAsync();

            var symptomAreas = request.SymptomAreas.Select(item => new AssessmentSymptomArea
            {
                SelfAssessmentId = selfAssessment.Id,
                Hand = item.Hand,
                PainfulPlace = item.PainfulPlace,
                Symptom = item.Symptom
            }).ToList();

            _context.AssessmentSymptomArea.AddRange(symptomAreas);

            var assessmentAnswers = request.BctqAnswerIds.Select(answerId => new AssessmentAnswer
            {
                SelfAssessmentId = selfAssessment.Id,
                BctqAnswerId = answerId
            }).ToList();

            _context.AssessmentAnswer.AddRange(assessmentAnswers);

            var physicalDetails = request.PhysicalDetails.Select(item => new AssessmentPhysicalDetail
            {
                SelfAssessmentId = selfAssessment.Id,
                PhysicalTestId = item.PhysicalTestId,
                IsPositive = item.IsPositive
            }).ToList();

            _context.AssessmentPhysicalDetail.AddRange(physicalDetails);

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            if (totalScore <= 34)
            {
                level = "Bình thường";
            }
            else if (totalScore > 34 && totalScore <= 54)
            {
                level = "Nhẹ";
            }
            else if (totalScore >= 55 && totalScore <= 74)
            {
                level = "Trung bình";
            }
            else
            {
                level = "Nặng";
            }

            return Ok(new
            {
                message = "Add successful",
                userId = userId,
                selfAssessmentId = selfAssessment.Id,
                totalScore = totalScore,
                level,
                physicalResults = physicalResults,
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return BadRequest(new
            {
                message = "Add failed",
                error = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

}