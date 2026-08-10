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
    [FromQuery] List<int>? ncsHands,
    [FromQuery] List<int>? ultrasoundHands,
    CancellationToken cancellationToken = default)
    {
        ncsHands ??= new List<int>();
        ultrasoundHands ??= new List<int>();

        var distinctNcsHands = ncsHands
            .Distinct()
            .ToHashSet();

        var distinctUltrasoundHands = ultrasoundHands
            .Distinct()
            .ToHashSet();

        // Mỗi tay chỉ tạo duy nhất một HandResult,
        // kể cả tay đó có cả NCS và siêu âm.
        var allHands = distinctNcsHands
            .Union(distinctUltrasoundHands)
            .Distinct()
            .ToList();

        if (allHands.Count == 0)
        {
            return BadRequest(
                "Phải chọn ít nhất một tay để thực hiện NCS hoặc siêu âm."
            );
        }

        // Giả sử quy ước:
        // 0 = tay trái
        // 1 = tay phải
        if (allHands.Any(hand => hand is not 0 and not 1))
        {
            return BadRequest(
                "Giá trị hand không hợp lệ. Chỉ chấp nhận 0 hoặc 1."
            );
        }

        var patientExists = await _context.Patients
            .AnyAsync(
                p => p.Id == patientId,
                cancellationToken
            );

        if (!patientExists)
        {
            return NotFound("Không tìm thấy bệnh nhân.");
        }

        // doctorUserId là users.id,
        // cần tìm staffs.id tương ứng.
        var doctor = await _context.Staffs
            .FirstOrDefaultAsync(
                s => s.UserId == doctorUserId,
                cancellationToken
            );

        if (doctor == null)
        {
            return NotFound("Không tìm thấy bác sĩ từ userId này.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken
            );

        try
        {
            // Bước 1: Tạo hồ sơ khám.
            var clinicalRecord = new ClinicalRecord
            {
                PatientId = patientId,
                DoctorId = doctor.Id,
                Time = DateTime.UtcNow
            };

            _context.ClinicalRecords.Add(clinicalRecord);

            await _context.SaveChangesAsync(cancellationToken);

            // Bước 2: Tạo một HandResult cho mỗi tay được chọn.
            var handResults = allHands
                .Select(hand => new HandResult
                {
                    ClinicalRecordId = clinicalRecord.Id,
                    Hand = hand,
                    Label = null,
                    Confidence = null,
                    Note = null,
                    Result = null
                })
                .ToList();

            _context.HandResults.AddRange(handResults);

            // Phải SaveChanges để EF tạo id cho từng HandResult.
            await _context.SaveChangesAsync(cancellationToken);

            // Map:
            // hand -> hand_result.id
            var handResultIdByHand = handResults
                .Where(x => x.Hand.HasValue)
                .ToDictionary(
                    x => x.Hand!.Value,
                    x => x.Id
                );

            // Bước 3: Tạo kết quả NCS cho những tay được chọn NCS.
            if (distinctNcsHands.Count > 0)
            {
                var ncsResults = distinctNcsHands
                    .Select(hand => new NcsResult
                    {
                        HandResultId = handResultIdByHand[hand],
                        Label = null,
                        Confidence = null,
                        Status = "Chưa xử lý"
                    })
                    .ToList();

                _context.NcsResults.AddRange(ncsResults);
            }

            // Bước 4: Tạo kết quả siêu âm cho những tay được chọn.
            if (distinctUltrasoundHands.Count > 0)
            {
                var ultrasoundResults = distinctUltrasoundHands
                    .Select(hand => new UltrasoundResult
                    {
                        HandResultId = handResultIdByHand[hand],
                        Label = null,
                        Confidence = null,
                        ImageUrl = null,
                        MaskUrl = null,
                        ContourPoints = null,
                        Status = "Chưa xử lý"
                    })
                    .ToList();

                _context.UltrasoundResults.AddRange(
                    ultrasoundResults
                );
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new
            {
                clinicalRecordId = clinicalRecord.Id,
                patientId = clinicalRecord.PatientId,
                doctorId = clinicalRecord.DoctorId,
                doctorUserId,
                time = clinicalRecord.Time,

                handResults = handResults.Select(handResult => new
                {
                    handResultId = handResult.Id,
                    hand = handResult.Hand,
                    hasNcs = handResult.Hand.HasValue &&
                             distinctNcsHands.Contains(
                                 handResult.Hand.Value
                             ),
                    hasUltrasound = handResult.Hand.HasValue &&
                                    distinctUltrasoundHands.Contains(
                                        handResult.Hand.Value
                                    )
                })
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);

            return BadRequest(new
            {
                message = "Tạo ca khám thất bại.",
                error = ex.InnerException?.Message
                        ?? ex.Message
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

            join h in _context.HandResults.AsNoTracking() on cr.Id equals h.ClinicalRecordId

            join d in _context.Staffs.AsNoTracking()
            on cr.DoctorId equals d.Id

            join p in _context.Patients.AsNoTracking()
                on cr.PatientId equals p.Id

            join nrTemp in _context.NcsResults.AsNoTracking()
                on h.Id equals nrTemp.HandResultId into ncsGroup
            from nr in ncsGroup.DefaultIfEmpty()

            join urTemp in _context.UltrasoundResults.AsNoTracking()
                on h.Id equals urTemp.HandResultId into ultrasoundGroup
            from ur in ultrasoundGroup.DefaultIfEmpty()

            where d.UserId == doctorUserId

            select new
            {
                clinicalRecordId = cr.Id,
                id = cr.Id,
                patientId = p.Id,
                patientName = p.Name,
                time = cr.Time,
                handResultId = h.Id,
                hand = h.Hand,
                result = h.Result,
                note = h.Note,

                ncsHand = nr == null
                ? (int?)null
                : h.Hand,

                ultrasoundHand = ur == null
                ? (int?)null
                : h.Hand
            }
        ).ToListAsync();

        var data = rows
            .GroupBy(x => new
            {
                x.id,
                x.patientId,
                x.patientName,
                x.time
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

                handResults = group
                .Select(x => new
                {
                    handResultId = x.handResultId,
                    hand = x.hand,
                    result = x.result,
                    note = x.note
                })
                .Distinct()
                .OrderBy(x => x.hand)
                .ToList()
            })
            .OrderByDescending(x => x.time)
            .ToList();

        return Ok(data);
    }

    [HttpGet("getResult")]
    public async Task<ActionResult> GetResult(
    [FromQuery] int clinicalRecordId)
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
                clinicalRecordId = cr.Id,
                patientId = p.Id,
                name = p.Name,
                dateBirth = p.DateBirth,
                time = cr.Time
            }
        ).FirstOrDefaultAsync();

        if (patient == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy hồ sơ khám."
            });
        }

        var results = await (
            from h in _context.HandResults.AsNoTracking()

            join nrTemp in _context.NcsResults.AsNoTracking()
                on h.Id equals nrTemp.HandResultId into ncsGroup
            from nr in ncsGroup.DefaultIfEmpty()

            join urTemp in _context.UltrasoundResults.AsNoTracking()
                on h.Id equals urTemp.HandResultId into ultrasoundGroup
            from ur in ultrasoundGroup.DefaultIfEmpty()

            where h.ClinicalRecordId == clinicalRecordId

            select new
            {
                handResultId = h.Id,
                hand = h.Hand,

                finalResult = new
                {
                    label = h.Label,
                    confidence = h.Confidence,
                    result = h.Result,
                    note = h.Note
                },

                ncs = nr == null
                    ? null
                    : new
                    {
                        id = nr.Id,
                        label = nr.Label,
                        confidence = nr.Confidence,
                        status = nr.Status
                    },

                ultrasound = ur == null
                    ? null
                    : new
                    {
                        id = ur.Id,
                        label = ur.Label,
                        confidence = ur.Confidence,
                        status = ur.Status
                    }
            }
        ).ToListAsync();

        return Ok(new
        {
            patient,
            results
        });
    }

    [HttpPost("confirmResult")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> ConfirmResult(
    [FromForm] int handResultId,
    [FromForm] string result,
    [FromForm] string? note
)
    {
        if (handResultId <= 0)
        {
            return BadRequest(new
            {
                message = "missing clinicalRecordId."
            });
        }

        if (string.IsNullOrWhiteSpace(result))
        {
            return BadRequest(new
            {
                message = "choose result."
            });
        }

        var handResult = await _context.HandResults
            .FirstOrDefaultAsync(x => x.Id == handResultId);

        if (handResult == null)
        {
            return NotFound(new
            {
                message = "not found clinical record."
            });
        }

        handResult.Result = result;

        handResult.Note = string.IsNullOrWhiteSpace(note)
            ? null
            : note.Trim();

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "ok.",

        });
    }


}