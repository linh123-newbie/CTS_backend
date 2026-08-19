using System.Diagnostics.Metrics;
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

    private string GetPhysicalTestMessage(string name, bool isPositive)
    {
        var testName = name.ToLower();

        if (testName.Contains("phalen"))
        {
            return isPositive
                ? "Bạn nên hạn chế gập cổ tay lâu, nghỉ tay thường xuyên và theo dõi cảm giác tê hoặc châm chích"
                : "Bình thường";
        }

        if (testName.Contains("tinel"))
        {
            return isPositive
                ? "Nên tránh tì đè mạnh vùng cổ tay, giữ cổ tay ở tư thế trung tính và theo dõi triệu chứng lan xuống các ngón tay"
                : "Bình thường";
        }

        if (testName.Contains("durkan"))
        {
            return isPositive
                ? "Bạn nên giảm các hoạt động gây áp lực lên cổ tay, nghỉ ngơi hợp lý và cân nhắc tham khảo bác sĩ nếu triệu chứng kéo dài"
                : "Bình thường";
        }

        return isPositive
            ? $"{name} dương tính."
            : $"{name} âm tính.";
    }

    [HttpGet("result")]
    public async Task<ActionResult> FinalResult([FromQuery] int selfAssessmentId)
    {
        var assessmentExist = await _context.SelfAssessment
            .AnyAsync(s => s.Id == selfAssessmentId);

        if (!assessmentExist)
        {
            return NotFound(new
            {
                message = "Self AssessmentId not found"
            });
        }

        var selfAssessment = await _context.SelfAssessment
            .Where(s => s.Id == selfAssessmentId)
            .Select(s => new
            {
                totalScore = s.Score,
                level = s.Level
            })
            .FirstOrDefaultAsync();

        var physicalDetails = await _context.AssessmentPhysicalDetail
            .Where(s => s.SelfAssessmentId == selfAssessmentId)
            .Include(s => s.PhysicalTest)
            .Select(s => new
            {
                physicalTestId = s.PhysicalTestId,
                name = s.PhysicalTest.Name,
                isPositive = s.IsPositive
            })
            .ToListAsync();

        var physicalResults = physicalDetails.Select(s => new
        {
            physicalTestId = s.physicalTestId,
            name = s.name,
            result = s.isPositive ? "Dương tính" : "Âm tính",
            message = GetPhysicalTestMessage(s.name, s.isPositive)
        }).ToList();

        return Ok(new
        {
            totalScore = selfAssessment!.totalScore,
            level = selfAssessment.level,
            physicalResults = physicalResults
        });
    }

    [HttpGet("clinical_record_result")]
    public async Task<ActionResult> ClinicalRecordResult([FromQuery] int userId)
    {
        var userExist = await _context.Users
            .AnyAsync(u => u.Id == userId);

        if (!userExist)
        {
            return NotFound(new
            {
                message = "User not found"
            });
        }

        var count = await (
            from p in _context.Patients
            join c in _context.ClinicalRecords
                on p.Id equals c.PatientId
            where p.UserId == userId
            select c.Id
        ).CountAsync();


        var latestRecord = await (
            from p in _context.Patients
            join c in _context.ClinicalRecords
                on p.Id equals c.PatientId
            where p.UserId == userId
            orderby c.Time descending, c.Id descending
            select new
            {
                c.Id,
                c.DoctorId,
                c.Time
            }
        ).FirstOrDefaultAsync();

        var latestTime = latestRecord?.Time?.ToString("dd-MM-yyyy HH:mm");

        if (latestRecord == null)
        {
            return Ok(new
            {
                count = 0,
                latestTime = (string?)null,
                doctorName = (string?)null,
                handResults = new List<object>()
            });
        }


        var allHandResults = await _context.HandResults
    .Where(h => h.ClinicalRecordId == latestRecord.Id)
    .Select(h => new
    {
        hand = h.Hand == 1 ? "Phải" : "Trái",

        result = h.Result == "bt"
            ? "Bình thường"
            : h.Result == "nhe"
                ? "Nhẹ"
                : h.Result == "tb"
                    ? "Trung bình"
                    : h.Result == "nang"
                        ? "Nặng"
                        : h.Result
    })
    .ToListAsync();


        string? doctorName = null;

        if (allHandResults.Any() &&
            allHandResults.All(h => h.result != null))
        {
            doctorName = await _context.Staffs
                .Where(s => s.Id == latestRecord.DoctorId)
                .Select(s => s.Name)
                .FirstOrDefaultAsync();
        }


        var handResults = allHandResults
            .Where(h => h.result != null)
            .ToList();



        return Ok(new
        {
            count,
            latestTime = latestTime,
            doctorName,
            handResults
        });
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

            }
            else if (totalScore <= 38)
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