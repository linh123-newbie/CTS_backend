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
                        HistogramUrl = null,
                        Csa = 0,
                        MeanIntensity = 0,
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



}