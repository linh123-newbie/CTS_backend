using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text.Json;
namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UltrasoundController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public UltrasoundController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("getUltrasoundResults")]
    public async Task<ActionResult> GetUltrasoundResults(
        [FromQuery] int doctorUserId)
    {
        var query =
            from n in _context.UltrasoundResults
            join c in _context.ClinicalRecords
                on n.ClinicalRecordId equals c.Id
            join p in _context.Patients
                on c.PatientId equals p.Id
            join s in _context.Staffs
                on c.DoctorId equals s.Id
            where s.UserId == doctorUserId
            select new
            {
                id = n.Id,
                clinicalRecordId = c.Id,
                patientId = p.Id,
                patientName = p.Name,
                hand = n.Hand,
                handText = n.Hand == 1 ? "Tay phải" : "Tay trái",
                label = n.Label,
                status = n.Status,
                time = c.Time
            };


        var data = await query
            .OrderByDescending(x => x.time)
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost("segment")]
    public async Task<ActionResult> SegmentImage(
        [FromForm] int ultrasoundResultId, IFormFile image)
    {
        if (image == null || image.Length == 0)
            return BadRequest("Vui lòng chọn ảnh siêu âm.");

        var ultrasoundResult = await _context.UltrasoundResults
            .FirstOrDefaultAsync(x => x.Id == ultrasoundResultId);

        if (ultrasoundResult == null)
            return NotFound("Không tìm thấy ultrasound result.");

        var client = _httpClientFactory.CreateClient("UltrasoundAi");

        using var form = new MultipartFormDataContent();

        using var stream = image.OpenReadStream();
        using var fileContent = new StreamContent(stream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(image.ContentType)
                ? "application/octet-stream"
                : image.ContentType
        );

        form.Add(fileContent, "image", image.FileName);

        // Python API vẫn tên là /predict, nhưng bên C# mình đặt là /segment
        var response = await client.PostAsync("predict", form);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            return StatusCode((int)response.StatusCode, errorText);
        }

        var json = await response.Content.ReadAsStringAsync();

        var segmentResult = JsonSerializer.Deserialize<UltrasoundSegmentResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (segmentResult == null || segmentResult.Success == false)
            return BadRequest("Python API trả về kết quả không hợp lệ.");

        ultrasoundResult.ImageUrl = segmentResult.OriginalUrl ?? "";
        ultrasoundResult.MaskUrl = segmentResult.PredMaskUrl ?? "";
        ultrasoundResult.Csa = segmentResult.CsaMm2;

        // Nếu Status là string:
        ultrasoundResult.Status = "SEGMENTED";

        // Nếu Status của bạn là int thì đổi thành:
        // ultrasoundResult.Status = 1;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            ultrasoundResultId = ultrasoundResult.Id,
            originalUrl = segmentResult.OriginalUrl,
            predMaskUrl = segmentResult.PredMaskUrl,
            markedUrl = segmentResult.MarkedUrl,
            csaMm2 = segmentResult.CsaMm2,
            status = ultrasoundResult.Status
        });
    }
}