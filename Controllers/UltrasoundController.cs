using CTS_backend.Data;
using CTS_backend.Services;
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
    private readonly IClinicalRecordResultService _clinicalRecordResultService;

    public UltrasoundController(AppDbContext context, IHttpClientFactory httpClientFactory, IClinicalRecordResultService clinicalRecordResultService)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _clinicalRecordResultService = clinicalRecordResultService;
    }

    [HttpGet("getUltrasoundResults")]
    public async Task<ActionResult> GetUltrasoundResults(
        [FromQuery] int doctorUserId)
    {
        var query =
            from n in _context.UltrasoundResults
            join h in _context.HandResults on n.HandResultId equals h.Id
            join c in _context.ClinicalRecords
                on h.ClinicalRecordId equals c.Id
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
                hand = h.Hand,
                handText = h.Hand == 1 ? "Tay phải" : "Tay trái",
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
        ultrasoundResult.ContourPoints = segmentResult.ContourPoints;
        ultrasoundResult.Label = null;
        ultrasoundResult.Confidence = null;

        // Nếu Status là string:
        ultrasoundResult.Status = "Đang xử lý";

        // Nếu Status của bạn là int thì đổi thành:
        // ultrasoundResult.Status = 1;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            ultrasoundResultId = ultrasoundResult.Id,
            originalUrl = segmentResult.OriginalUrl,
            predMaskUrl = segmentResult.PredMaskUrl,
            // markedUrl = segmentResult.MarkedUrl,
            status = ultrasoundResult.Status,
            contourPoints = segmentResult.ContourPoints,
        });
    }

    [HttpPost("result")]
    public async Task<ActionResult> Result([FromBody] UltrasoundResultRequest request)
    {
        var ultrasoundResult = await _context.UltrasoundResults
            .FirstOrDefaultAsync(x => x.Id == request.UltrasoundResultId);

        if (ultrasoundResult == null)
        {
            return NotFound(new
            {
                message = "not found ultrasound result"
            });
        }

        var client = _httpClientFactory.CreateClient("UltrasoundAi");

        var response = await client.PostAsJsonAsync("result", new
        {
            originalUrl = request.OriginalUrl,
            predMaskUrl = request.PredMaskUrl,
        });


        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new
            {
                message = "Python API result trả lỗi.",
                detail = json
            });
        }

        var result =
        JsonSerializer.Deserialize<PythonUltrasoundResultResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result == null)
        {
            return StatusCode(500, new
            {
                message = "Không parse được response từ Python API."
            });
        }

        var finalLabel = result.ImagePrediction?.Label;
        var finalConfidence = result.ImagePrediction?.Confidence ?? 0;

        ultrasoundResult.ImageUrl = request.OriginalUrl;
        ultrasoundResult.MaskUrl = request.PredMaskUrl;
        ultrasoundResult.Label = finalLabel;
        ultrasoundResult.Confidence = finalConfidence;
        ultrasoundResult.ContourPoints = request.ContourPoints;
        ultrasoundResult.Status = "Đang xử lý";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            ultrasoundResultId = ultrasoundResult.Id,

            imageUrl = ultrasoundResult.ImageUrl,
            maskUrl = ultrasoundResult.MaskUrl,
            label = finalLabel,
            confidence = finalConfidence,
            contourPoints = ultrasoundResult.ContourPoints,
            status = ultrasoundResult.Status,
            imagePrediction = result.ImagePrediction,

        });
    }

    [HttpPost("update_contour")]
    public async Task<ActionResult> UpdateContour([FromBody] ContoursRequest request)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Request không hợp lệ."
            });
        }

        if (request.Contours == null || request.Contours.Count < 3)
        {
            return BadRequest(new
            {
                message = "Cần ít nhất 3 điểm contour."
            });
        }

        var ultrasoundResult = await _context.UltrasoundResults
            .FirstOrDefaultAsync(x => x.Id == request.UltrasoundResultId);

        if (ultrasoundResult == null)
        {
            return NotFound(new
            {
                message = "Lỗi, ko tìm thấy kết quả."
            });
        }

        var client = _httpClientFactory.CreateClient("UltrasoundAi");

        HttpResponseMessage pythonResponse;

        try
        {
            pythonResponse = await client.PostAsJsonAsync("update_contour", new
            {
                originalUrl = ultrasoundResult.ImageUrl,
                contours = request.Contours.Select(point => new
                {
                    x = point.X,
                    y = point.Y,
                    kind = point.Kind
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Không gọi được API update_contour bên Python.",
                error = ex.Message
            });
        }

        var json = await pythonResponse.Content.ReadAsStringAsync();

        if (!pythonResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)pythonResponse.StatusCode, new
            {
                message = "Python API update_contour trả lỗi.",
                detail = json
            });
        }

        var contoursResponse = JsonSerializer.Deserialize<ContoursResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        if (contoursResponse == null)
        {
            return StatusCode(500, new
            {
                message = "Không parse được response từ Python update_contour.",
                raw = json
            });
        }

        ultrasoundResult.MaskUrl = contoursResponse.PredMaskUrl;
        ultrasoundResult.ContourPoints = request.Contours;
        ultrasoundResult.Label = null;
        ultrasoundResult.Confidence = null;
        // ultrasoundResult.Status = "Đang xử lý";
        // ultrasoundResult.Status = "SEGMENTED";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            // success = true,
            // ultrasoundResultId = ultrasoundResult.Id,
            pred_mask_url = contoursResponse.PredMaskUrl,
            contourPoints = request.Contours,
            // areaPx = calCsaResult.AreaPx,
            // status = ultrasoundResult.Status
        });
    }

    [HttpPost("confirm")]
    public async Task<ActionResult> Confirm(
    [FromForm] int ultrasoundResultId,
    [FromForm] string status
)
    {
        if (ultrasoundResultId <= 0)
        {
            return BadRequest("Missing ultrasound result id.");
        }

        var ultrasound = await _context.UltrasoundResults
            .FirstOrDefaultAsync(x => x.Id == ultrasoundResultId);

        if (ultrasound == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy kết quả siêu âm."
            });
        }

        ultrasound.Status = status.Trim();

        await _context.SaveChangesAsync();



        var handResultUpdated =
            await _clinicalRecordResultService.UpdateIfReadyAsync(
                ultrasound.HandResultId
            );

        return Ok(new
        {
            message = "Confirm successfully.",

        });
    }

    [HttpGet("show")]
    public async Task<IActionResult> Show([FromQuery] int ultrasoundResultId)
    {
        var ultrasound = await _context.UltrasoundResults
            .FirstOrDefaultAsync(u => u.Id == ultrasoundResultId);

        if (ultrasound == null)
        {
            return NotFound(new
            {
                message = "Display error."
            });
        }

        return Ok(new
        {
            ultrasound.Id,
            ultrasound.ImageUrl,
            ultrasound.MaskUrl,
            ultrasound.Label,
            ultrasound.Confidence,
            ultrasound.Status,
            ultrasound.ContourPoints
        });
    }
}

