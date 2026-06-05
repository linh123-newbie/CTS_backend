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

            var selfAssessment = new SelfAssessment
            {
                UserId = userId,
                Time = DateTime.UtcNow,
                Score = null,
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

            return Ok(new
            {
                message = "Add successful",
                userId = userId,
                selfAssessmentId = selfAssessment.Id,
                symptomAreaCount = symptomAreas.Count,
                answerCount = assessmentAnswers.Count,
                physicalDetailCount = physicalDetails.Count
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