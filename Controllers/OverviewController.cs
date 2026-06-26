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
}
