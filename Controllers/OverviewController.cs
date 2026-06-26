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
            join cr in _context.ClinicalRecords
                on nr.ClinicalRecordId equals cr.Id
            where cr.DoctorId == staffId
                  && nr.Status == "Chưa xử lý"
            select nr
        ).CountAsync();

        // 3. Số siêu âm chưa xử lý
        var pendingUltrasoundCount = await (
            from ur in _context.UltrasoundResults
            join cr in _context.ClinicalRecords
                on ur.ClinicalRecordId equals cr.Id
            where cr.DoctorId == staffId
                  && ur.Status == "Chưa xử lý"
            select ur
        ).CountAsync();

        // 4. Số ca khám chưa có kết luận/result
        var emptyClinicalResultCount = await _context.ClinicalRecords
            .Where(cr => cr.DoctorId == staffId)
            .Where(cr => cr.Result == null || cr.Result == "")
            .CountAsync();

        return Ok(new
        {
            todayClinicalRecordCount,
            pendingNcsCount,
            pendingUltrasoundCount,
            emptyClinicalResultCount
        });
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
            join n in _context.NcsResults on c.Id equals n.ClinicalRecordId
            where s.UserId == doctorUserId
                  && c.Time >= today
                  && c.Time < tomorrow
            select new
            {
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,
                examType = "NCS",
                status = n.Status,
                handText = n.Hand == 1 ? "Tay phải" :
                           n.Hand == 0 ? "Tay trái" : null
            };

        var ultrasoundQuery =
            from c in _context.ClinicalRecords
            join s in _context.Staffs on c.DoctorId equals s.Id
            join p in _context.Patients on c.PatientId equals p.Id
            join ur in _context.UltrasoundResults on c.Id equals ur.ClinicalRecordId
            where s.UserId == doctorUserId
                  && c.Time >= today
                  && c.Time < tomorrow
            select new
            {
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,
                examType = "Siêu âm",
                status = ur.Status,
                handText = ur.Hand == 1 ? "Tay phải" :
                           ur.Hand == 0 ? "Tay trái" : null
            };

        var rawData = await ncsQuery
            .Concat(ultrasoundQuery)
            .OrderByDescending(x => x.time)
            .ThenBy(x => x.patientId)
            .ThenBy(x => x.examType)
            .ToListAsync();

        var data = rawData.Select(x => new
        {
            time = x.time.ToString("HH:mm"),
            patientId = x.patientId,
            patientName = x.patientName,
            examType = x.examType,
            status = FormatStatus(x.status),
            handText = x.handText
        });

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
        var emptyClinicalResultCount = await _context.ClinicalRecords
            .CountAsync(cr =>
                staffIds.Contains(cr.DoctorId) &&
                (cr.Result == null || cr.Result == "")
            );

        // 2. NCS chờ xác nhận
        var confirmNcsCount = await (
            from nr in _context.NcsResults
            join cr in _context.ClinicalRecords
                on nr.ClinicalRecordId equals cr.Id
            where staffIds.Contains(cr.DoctorId)
                  && nr.Status == "CONFIRM"
            select nr
        ).CountAsync();

        // 3. Siêu âm chờ xác nhận
        var confirmUltrasoundCount = await (
            from ur in _context.UltrasoundResults
            join cr in _context.ClinicalRecords
                on ur.ClinicalRecordId equals cr.Id
            where staffIds.Contains(cr.DoctorId)
                  && ur.Status == "CONFIRM"
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
        if (take <= 0)
        {
            take = 10;
        }

        var ncsQuery =
            from c in _context.ClinicalRecords
            join s in _context.Staffs on c.DoctorId equals s.Id
            join p in _context.Patients on c.PatientId equals p.Id
            join n in _context.NcsResults on c.Id equals n.ClinicalRecordId
            where s.UserId == doctorUserId
                  && c.Result != null
                  && c.Result.Trim() != ""
            select new
            {
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,
                examType = "NCS",
                label = n.Label,
                hand = (int?)n.Hand,
                handText = n.Hand == 1 ? "Tay phải" :
                           n.Hand == 0 ? "Tay trái" : null,
                clinicalResult = c.Result
            };

        var ultrasoundQuery =
            from c in _context.ClinicalRecords
            join s in _context.Staffs on c.DoctorId equals s.Id
            join p in _context.Patients on c.PatientId equals p.Id
            join ur in _context.UltrasoundResults on c.Id equals ur.ClinicalRecordId
            where s.UserId == doctorUserId
                  && c.Result != null
                  && c.Result.Trim() != ""
            select new
            {
                time = c.Time,
                patientId = p.Id,
                patientName = p.Name,
                examType = "Siêu âm",
                label = ur.Label,
                hand = (int?)ur.Hand,
                handText = ur.Hand == 1 ? "Tay phải" :
                           ur.Hand == 0 ? "Tay trái" : null,
                clinicalResult = c.Result
            };

        var rawData = await ncsQuery
            .Concat(ultrasoundQuery)
            .OrderByDescending(x => x.time)
            .ThenBy(x => x.patientId)
            .ThenBy(x => x.examType)
            .ThenBy(x => x.hand)
            .Take(take)
            .ToListAsync();

        var data = rawData.Select(x => new
        {
            patientId = x.patientId,
            name = x.patientName,
            loaiKham = x.examType,
            label = x.label,
            hand = x.handText,
            clinicalResult = x.clinicalResult
        });

        return Ok(data);
    }
}
