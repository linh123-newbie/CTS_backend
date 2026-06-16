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
    [FromQuery] int doctorId,
    [FromQuery] int patientId,
    [FromQuery] List<int> ncsHands,
    [FromQuery] List<int> ultrasoundHands)
    {
        // kiểm tra patient có tồn tại không
        var patientExists = await _context.Patients.AnyAsync(p => p.Id == patientId);
        if (!patientExists)
            return NotFound("Không tìm thấy bệnh nhân");

        // kiểm tra doctor có tồn tại không
        var doctorExists = await _context.Staffs.AnyAsync(s => s.Id == doctorId);
        if (!doctorExists)
            return NotFound("Không tìm thấy bác sĩ");

        using var transaction = await _context.Database.BeginTransactionAsync();


        try
        {
            // 1. Tạo ClinicalRecord
            var clinicalRecord = new ClinicalRecord
            {
                PatientId = patientId,
                DoctorId = doctorId,
                Time = DateTime.UtcNow,
                Result = ""
            };

            _context.ClinicalRecords.Add(clinicalRecord);
            await _context.SaveChangesAsync();

            // 2. Tạo NCS result, mỗi tay 1 dòng
            if (ncsHands != null && ncsHands.Count > 0)
            {
                var ncsResults = ncsHands.Select(hand => new NcsResult
                {
                    ClinicalRecordId = clinicalRecord.Id,
                    Hand = hand,
                    Label = null
                }).ToList();

                _context.NcsResults.AddRange(ncsResults);
            }

            // 3. Tạo Ultrasound result, mỗi tay 1 dòng
            if (ultrasoundHands != null && ultrasoundHands.Count > 0)
            {
                var ultrasoundResults = ultrasoundHands.Select(hand => new UltrasoundResult
                {
                    ClinicalRecordId = clinicalRecord.Id,
                    Hand = hand,
                    Label = null,
                    ImageUrl = null,
                    MaskUrl = null,
                    HistogramUrl = null,
                    Csa = 0,
                    MeanIntensity = 0
                }).ToList();

                _context.UltrasoundResults.AddRange(ultrasoundResults);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            var data = new
            {
                clinicalRecordId = clinicalRecord.Id,
                patientId = clinicalRecord.PatientId,
                doctorId = clinicalRecord.DoctorId,
                time = clinicalRecord.Time,
                ncsHands = ncsHands,
                ultrasoundHands = ultrasoundHands
            };

            return Ok(data);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return BadRequest(new
            {
                message = "Tạo ca khám thất bại",
                error = ex.Message
            });
        }

    }



}