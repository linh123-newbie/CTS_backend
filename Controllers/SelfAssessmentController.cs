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

    [HttpPost("create_self_assessment")]
    public async Task<ActionResult> CreateSelfAssessment([FromQuery] int userId, [FromBody] CreateSelfAssessmentRequest request)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var userExist = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExist)
            {
                return NotFound(new
                {
                    message = "User not found",
                });
            }
            var selfAssessment = new SelfAssessment
            {
                UserId = userId,
                Time = DateTime.UtcNow
            };
            _context.SelfAssessment.Add(selfAssessment);
            await _context.SaveChangesAsync();

            int selfAssessmentId = selfAssessment.Id;
            foreach (var symptom in request.SymptomAreas)
            {
                var symptomArea = new AssessmentSymptomArea
                {
                    SelfAssessmentId = selfAssessmentId,
                    Hand = symptom.Hand,
                    PainfulPlace = symptom.PainfulPlace,
                    Symptom = symptom.Symptom
                };
                _context.AssessmentSymptomArea.Add(symptomArea);
            }
            foreach (int answerId in request.BctqAnswerIds)
            {
                var answer = new AssessmentAnswer
                {
                    SelfAssessmentId = selfAssessmentId,
                    BctqAnswerId = answerId
                };
                _context.AssessmentAnswer.Add(answer);
            }
            foreach (var physical in request.PhysicalDetails)
            {
                var physicalResult = new AssessmentPhysicalDetail
                {
                    SelfAssessmentId = selfAssessmentId,
                    PhysicalTestId = physical.PhysicalTestId,
                    IsPositive = physical.IsPositive

                };
                _context.AssessmentPhysicalDetail.Add(physicalResult);
            }
            await _context.SaveChangesAsync();

            var totalScore = await _context.BctqAnswers.Where(a => request.BctqAnswerIds.Contains(a.Id)).SumAsync(a => a.Rate);
            selfAssessment.Score = totalScore;
            if (totalScore <= 19)
            {
                selfAssessment.Level = "Bình thường";

            }else if (totalScore <= 38)
            {
                selfAssessment.Level = "Nhẹ";
            }
            else if (totalScore <= 57)
            {
                selfAssessment.Level = "Trung bình";
            }
            else if (totalScore <= 76)
            {
                selfAssessment.Level = "Nặng";
            }
            else
            {
                selfAssessment.Level = "Rất nặng";
            }
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return Ok(new
            {
                selfAssessmentId = selfAssessmentId,
            });

        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return BadRequest(new
            {
                message = "Add failed",
            });
        }
    }

}