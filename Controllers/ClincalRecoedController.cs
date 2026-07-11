using CTS_backend.Data;
using CTS_backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicalRecordController : ControllerBase
{
    private readonly AppDbContext _context;

    public ClinicalRecordController(AppDbContext context)
    {
        _context = context;
    }


    [HttpPost("addClinicalRecord")]
    public async Task<ActionResult> AddClinicalRecord(
    [FromQuery] int doctorUserId,
    [FromQuery] int patientId,
    [FromQuery] List<int> ncsHands,
    [FromQuery] List<int> ultrasoundHands)
    {
        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId);
        if (!patientExists)
            return NotFound("Không tìm thấy bệnh nhân");

        // doctorUserId là users.id
        // cần tìm staff tương ứng
        var doctor = await _context.Staffs
            .FirstOrDefaultAsync(s => s.UserId == doctorUserId);

        if (doctor == null)
            return NotFound("Không tìm thấy bác sĩ từ userId này");

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var clinicalRecord = new ClinicalRecord
            {
                PatientId = patientId,
                DoctorId = doctor.Id, // Lưu staffs.id, không lưu users.id
                Time = DateTime.UtcNow,
                Result = ""
            };

            _context.ClinicalRecords.Add(clinicalRecord);
            await _context.SaveChangesAsync();

            if (ncsHands != null && ncsHands.Count > 0)
            {
                var ncsResults = ncsHands
                    .Distinct()
                    .Select(hand => new NcsResult
                    {
                        ClinicalRecordId = clinicalRecord.Id,
                        Hand = hand,
                        Label = null,
                        Status = "Chưa xử lý"
                    })
                    .ToList();

                _context.NcsResults.AddRange(ncsResults);
            }

            if (ultrasoundHands != null && ultrasoundHands.Count > 0)
            {
                var ultrasoundResults = ultrasoundHands
                    .Distinct()
                    .Select(hand => new UltrasoundResult
                    {
                        ClinicalRecordId = clinicalRecord.Id,
                        Hand = hand,
                        Label = null,
                        ImageUrl = null,
                        MaskUrl = null,
                        Csa = 0,
                        Status = "Chưa xử lý",
                    })
                    .ToList();

                _context.UltrasoundResults.AddRange(ultrasoundResults);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                clinicalRecordId = clinicalRecord.Id,
                patientId = clinicalRecord.PatientId,
                doctorId = clinicalRecord.DoctorId,       // staffs.id
                doctorUserId = doctorUserId,              // users.id
                time = clinicalRecord.Time,
                ncsHands,
                ultrasoundHands
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();

            return BadRequest(new
            {
                message = "Tạo ca khám thất bại",
                error = ex.InnerException?.Message ?? ex.Message
            });
        }
    }

    [HttpGet("getResults")]
    public async Task<ActionResult> GetResults([FromQuery] int doctorUserId)
    {
        if (doctorUserId <= 0)
        {
            return BadRequest("missing doctorId.");
        }

        var rows = await (
            from cr in _context.ClinicalRecords.AsNoTracking()

            join d in _context.Staffs.AsNoTracking()
            on cr.DoctorId equals d.Id

            join p in _context.Patients.AsNoTracking()
                on cr.PatientId equals p.Id

            join nrTemp in _context.NcsResults.AsNoTracking()
                on cr.Id equals nrTemp.ClinicalRecordId into ncsGroup
            from nr in ncsGroup.DefaultIfEmpty()

            join urTemp in _context.UltrasoundResults.AsNoTracking()
                on cr.Id equals urTemp.ClinicalRecordId into ultrasoundGroup
            from ur in ultrasoundGroup.DefaultIfEmpty()

            where d.UserId == doctorUserId

            select new
            {
                id = cr.Id,
                patientId = p.Id,
                patientName = p.Name,
                time = cr.Time,
                result = cr.Result ?? string.Empty,

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
                x.result
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

                result = group.Key.result
            })
            .OrderByDescending(x => x.time)
            .ToList();

        return Ok(data);
    }

    [HttpGet("getResult")]
    public async Task<ActionResult> GetResult([FromQuery] int clinicalRecordId)
    {
        if (clinicalRecordId <= 0)
        {
            return BadRequest("missing clinicalRecordId.");
        }

        var patient = await (
            from cr in _context.ClinicalRecords.AsNoTracking()

            join p in _context.Patients.AsNoTracking()
                on cr.PatientId equals p.Id

            where cr.Id == clinicalRecordId

            select new
            {
                id = p.Id,
                name = p.Name,
                dateBirth = p.DateBirth,
                time = cr.Time,
                
            }
        ).ToListAsync();

        return Ok(new
        {
            patient
        });
    }

}