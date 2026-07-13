using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class OverviewController : ControllerBase
{
    private readonly AppDbContext _context;
    public OverviewController(AppDbContext context, IHttpClientFactory httpClientFactory, HttpClient httpClient)
    {
        _context = context;
    }
    [HttpGet("getCount")]
    public async Task<ActionResult> GetCount([FromQuery] int doctorUserId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var staffId = await _context.Staffs
        .Where(s => s.UserId == doctorUserId)
        .Select(s => s.Id)
        .FirstOrDefaultAsync();

        if (staffId == 0)
        {
            return NotFound(new
            {
                message = "Không tìm thấy bác sĩ tương ứng với user này."
            });
        }

        var todayClinicalRecordCount = await _context.ClinicalRecords
        .Where(cr => cr.DoctorId == staffId)
        .Where(cr => cr.Time >= today && cr.Time < tomorrow)
        .CountAsync();

        // 2. Số NCS chưa xử lý
        var pendingNcsCount = await (
            from nr in _context.NcsResults
            join cr in _context.HandResults on nr.HandResultId equals cr.Id
            join cl in _context.ClinicalRecords on cr.ClinicalRecordId equals cl.Id
            where cl.DoctorId == staffId
                  && nr.Status == "Chưa xử lý"
            select nr
        ).CountAsync();

        // 3. Số siêu âm chưa xử lý
        var pendingUltrasoundCount = await (
            from u in _context.UltrasoundResults
            join cu in _context.HandResults on u.HandResultId equals cu.Id
            join clu in _context.ClinicalRecords on cu.ClinicalRecordId equals clu.Id
            where clu.DoctorId == staffId
                  && u.Status == "Chưa xử lý"
            select u
        ).CountAsync();

        // 4. Số ca khám chưa có kết luận/result
        var emptyClinicalResultCount = await (
         from c in _context.ClinicalRecords
         join h in _context.HandResults on c.Id equals h.ClinicalRecordId
         where c.DoctorId == staffId && (h.Result == null || h.Result == "")
         select c
        ).CountAsync();

        return Ok(new
        {
            todayClinicalRecordCount,
            pendingNcsCount,
            pendingUltrasoundCount,
            emptyClinicalResultCount
        });
    }

    private static string GetOverallSessionStatus(
    IEnumerable<string?> statuses)
    {
        var values = statuses
            .Select(FormatStatus)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        if (values.Count == 0)
        {
            return "Chưa xử lý";
        }

        if (values.All(x => x == "Đã xử lý"))
        {
            return "Hoàn tất";
        }

        if (values.Any(x => x == "Chờ xác nhận"))
        {
            return "Chờ xác nhận";
        }

        if (values.Any(x => x == "Đang xử lý"))
        {
            return "Đang xử lý";
        }

        return "Chưa xử lý";
    }

    [HttpGet("getSession")]
    public async Task<ActionResult> GetSession([FromQuery] int doctorUserId)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var ncsQuery =
            from c in _context.ClinicalRecords
            join s in _context.Staffs on c.DoctorId equals s.Id
            join p in _context.Patients on c.PatientId equals p.Id
            join h in _context.HandResults on c.Id equals h.ClinicalRecordId
            join n in _context.NcsResults on h.Id equals n.HandResultId
            where s.UserId == doctorUserId
                  && c.Time >= today
                  && c.Time < tomorrow
            select new
            {
                clinicalRecordId = c.Id,
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,
                examType = "NCS",
                status = n.Status,
                handText = h.Hand == 1 ? "Tay phải" :
                           h.Hand == 0 ? "Tay trái" : null
            };

        var ultrasoundQuery =
            from c in _context.ClinicalRecords
            join s in _context.Staffs on c.DoctorId equals s.Id
            join p in _context.Patients on c.PatientId equals p.Id
            join h in _context.HandResults on c.Id equals h.ClinicalRecordId
            join ur in _context.UltrasoundResults on h.Id equals ur.HandResultId
            where s.UserId == doctorUserId
                  && c.Time >= today
                  && c.Time < tomorrow
            select new
            {
                clinicalRecordId = c.Id,
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,
                examType = "Siêu âm",
                status = ur.Status,
                handText = h.Hand == 1 ? "Tay phải" :
                           h.Hand == 0 ? "Tay trái" : null
            };

        var rawData = await ncsQuery
            .Concat(ultrasoundQuery)
            .ToListAsync();

        var data = rawData
            .GroupBy(x => new
            {
                x.clinicalRecordId,
                x.time,
                x.patientId,
                x.patientName
            })
            .OrderByDescending(g => g.Key.time)
            .ThenBy(g => g.Key.patientId)
            .Select(g =>
{
    var ncsHands = g
        .Where(x => x.examType == "NCS")
        .Select(x => x.handText)
        .Where(x => x != null)
        .Distinct()
        .ToList();

    var ultrasoundHands = g
        .Where(x => x.examType == "Siêu âm")
        .Select(x => x.handText)
        .Where(x => x != null)
        .Distinct()
        .ToList();

    return new
    {
        clinicalRecordId = g.Key.clinicalRecordId,
        time = g.Key.time?.ToString("HH:mm"),
        patientId = g.Key.patientId,
        patientCode = $"BN{g.Key.patientId:D5}",
        patientName = g.Key.patientName,

        ncs = ncsHands,
        ultrasound = ultrasoundHands,

        status = GetOverallSessionStatus(
            g.Select(x => x.status)
        )
    };
})
            .ToList();

        return Ok(data);
    }

    private static string? FormatStatus(string? status)
    {
        return status switch
        {
            "PROCESSING" => "Đang xử lý",
            "SEGMENTED" => "Đang xử lý",
            "CONFIRM" => "Chờ xác nhận",
            _ => status
        };
    }

    [HttpGet("handlingCases")]
    public async Task<ActionResult> HandlingCases([FromQuery] int doctorUserId)
    {
        var staffIds = await _context.Staffs
            .Where(s => s.UserId == doctorUserId)
            .Select(s => s.Id)
            .ToListAsync();

        if (staffIds.Count == 0)
        {
            return NotFound(new
            {
                message = "Không tìm thấy staff tương ứng với user này."
            });
        }

        // 1. Ca khám chưa có kết luận/result
        var emptyClinicalResultCount = await (
    from h in _context.HandResults.AsNoTracking()
    join cr in _context.ClinicalRecords.AsNoTracking()
        on h.ClinicalRecordId equals cr.Id
    where staffIds.Contains(cr.DoctorId)
          && (h.Result == null || h.Result == "")
    select cr.Id
)
.Distinct()
.CountAsync();

        // 2. NCS chờ xác nhận
        var confirmNcsCount = await (
            from nr in _context.NcsResults
            join h in _context.HandResults on nr.HandResultId equals h.Id
            join cr in _context.ClinicalRecords
                on h.ClinicalRecordId equals cr.Id
            where staffIds.Contains(cr.DoctorId)
                  && nr.Status == "Đang xử lý"
            select nr
        ).CountAsync();

        // 3. Siêu âm chờ xác nhận
        var confirmUltrasoundCount = await (
            from ur in _context.UltrasoundResults
            join h in _context.HandResults on ur.HandResultId equals h.Id
            join cr in _context.ClinicalRecords
                on h.ClinicalRecordId equals cr.Id
            where staffIds.Contains(cr.DoctorId)
                  && ur.Status == "Đang xử lý"
            select ur
        ).CountAsync();

        return Ok(new
        {
            emptyClinicalResultCount,
            confirmNcsCount,
            confirmUltrasoundCount,

        });
    }

    [HttpGet("getRecentSession")]
    public async Task<ActionResult> GetRecentSession(
    [FromQuery] int doctorUserId,
    [FromQuery] int take = 10)
    {
        if (doctorUserId <= 0)
        {
            return BadRequest(new
            {
                message = "Missing doctorUserId."
            });
        }

        if (take <= 0)
        {
            take = 10;
        }

        var rows = await (
            from c in _context.ClinicalRecords.AsNoTracking()

            join s in _context.Staffs.AsNoTracking()
                on c.DoctorId equals s.Id

            join p in _context.Patients.AsNoTracking()
                on c.PatientId equals p.Id

            join h in _context.HandResults.AsNoTracking()
                on c.Id equals h.ClinicalRecordId

            where s.UserId == doctorUserId
                  && h.Result != null
                  && h.Result.Trim() != ""

            select new
            {
                clinicalRecordId = c.Id,
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,

                handResultId = h.Id,
                hand = h.Hand,
                result = h.Result
            }
        ).ToListAsync();

        var sessions = rows
            .GroupBy(x => new
            {
                x.clinicalRecordId,
                x.time,
                x.patientId,
                x.patientName
            })
            .Select(group => new
            {
                id = group.Key.clinicalRecordId,
                time = group.Key.time,
                patientId = group.Key.patientId,
                patientName = group.Key.patientName,

                finalResults = group
                    .Select(x => new
                    {
                        handResultId = x.handResultId,
                        hand = x.hand,
                        result = x.result
                    })
                    .OrderBy(x => x.hand)
                    .ToList()
            })
            .OrderByDescending(x => x.time)
            .Take(take)
            .ToList();

        return Ok(sessions);
    }
}
