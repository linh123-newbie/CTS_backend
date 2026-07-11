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
            id = latest.Id,
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

    [HttpGet("assessmentsTime")]
    public async Task<ActionResult> GetAllAssessmentsTime([FromQuery] int userId)
    {
        var latest = await _context.SelfAssessment
        .Where(u => u.UserId == userId)
        .OrderBy(t => t.Time)
        .Select(t => new
        {
            id = t.Id,
            time = t.Time.ToString("dd/MM/yyyy HH:mm"),
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

    [HttpGet("detailAssessment")]
    public async Task<ActionResult> GetDetailAssessment([FromQuery] int userId, [FromQuery] int SelfAssessmentId)
    {
        var assessment = await _context.SelfAssessment
            .Where(u => u.UserId == userId && u.Id == SelfAssessmentId)
            .FirstOrDefaultAsync();

        if (assessment == null)
        {
            return Ok(null);
        }

        var answers = await _context.AssessmentAnswer
            .Where(aa => aa.SelfAssessmentId == assessment.Id)
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
                (x, bq) => new
                {
                    questionTypeId = bq.QuestionTypeId,
                    question = bq.Content,
                    rate = x.ba.Rate,
                    answerContent = x.ba.AnswerContent
                }
            )
            .ToListAsync();

        var symptomAnswers = answers
            .Where(x => x.questionTypeId == 1)
            .Select(x => new
            {
                question = x.question,
                rate = x.rate,
                answerContent = x.answerContent
            })
            .ToList();

        var functionAnswers = answers
            .Where(x => x.questionTypeId == 2)
            .Select(x => new
            {
                question = x.question,
                rate = x.rate,
                answerContent = x.answerContent
            })
            .ToList();

        var symptomScore = symptomAnswers.Sum(x => x.rate);
        var functionScore = functionAnswers.Sum(x => x.rate);

        var physicalResults = await _context.AssessmentPhysicalDetail
            .Where(pd => pd.SelfAssessmentId == assessment.Id)
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
            symptom = new
            {
                score = symptomScore,
                totalScore = symptomAnswers.Count * 5,
                answers = symptomAnswers
            },

            function = new
            {
                score = functionScore,
                totalScore = functionAnswers.Count * 5,
                answers = functionAnswers
            },

            physicalResults = physicalResults
        };

        return Ok(data);
    }

    [HttpGet("getResults")]
    public async Task<ActionResult> GetResults([FromQuery] int doctorId)
    {
        if (doctorId <= 0)
        {
            return BadRequest("Thiếu doctorId.");
        }

        var rows = await (
            from cr in _context.ClinicalRecords.AsNoTracking()

            join p in _context.Patients.AsNoTracking()
                on cr.PatientId equals p.Id

            join nrTemp in _context.NcsResults.AsNoTracking()
                on cr.Id equals nrTemp.ClinicalRecordId into ncsGroup
            from nr in ncsGroup.DefaultIfEmpty()

            join urTemp in _context.UltrasoundResults.AsNoTracking()
                on cr.Id equals urTemp.ClinicalRecordId into ultrasoundGroup
            from ur in ultrasoundGroup.DefaultIfEmpty()

            where cr.DoctorId == doctorId

            select new
            {
                id = cr.Id,
                patientId = p.Id,
                patientName = p.Name,
                time = cr.Time,
                label = cr.Label,

                ncsHand = nr == null
                    ? (int?)null
                    : nr.Hand,

                ultrasoundHand = ur == null
                    ? (int?)null
                    : ur.Hand
            }
        ).ToListAsync();

        var data = rows
            .GroupBy(x => new
            {
                x.id,
                x.patientId,
                x.patientName,
                x.time,
                x.label
            })
            .Select(group => new
            {
                id = group.Key.id,
                patientId = group.Key.patientId,
                patientName = group.Key.patientName,
                time = group.Key.time,

                ncsHands = group
                    .Where(x => x.ncsHand.HasValue)
                    .Select(x => x.ncsHand!.Value)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),

                ultrasoundHands = group
                    .Where(x => x.ultrasoundHand.HasValue)
                    .Select(x => x.ultrasoundHand!.Value)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList(),

                label = group.Key.label
            })
            .OrderByDescending(x => x.time)
            .ToList();

        return Ok(data);
    }

}