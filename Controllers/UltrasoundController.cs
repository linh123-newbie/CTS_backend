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

    public UltrasoundController(AppDbContext context, IHttpClientFactory httpClientFactory, HttpClient httpClient)
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
        ultrasoundResult.Perimeter = segmentResult.Perimeter;
        ultrasoundResult.FlatteningRatio = segmentResult.FlatteningRatio;
        ultrasoundResult.Circularity = segmentResult.Circularity;
        ultrasoundResult.ContourPoints = segmentResult.ContourPoints;

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
            markedUrl = segmentResult.MarkedUrl,
            csaMm2 = segmentResult.CsaMm2,
            perimeter = segmentResult.Perimeter,
            flatteningRatio = segmentResult.FlatteningRatio,
            circularity = segmentResult.Circularity,
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
            csaMm2 = request.CsaMm2,
            perimeter = request.Perimeter,
            flattening_ratio = request.FlatteningRatio,
            circularity = request.Circularity,
            // contour_points = request.ContourPoints
        });

        var json = await response.Content.ReadAsStringAsync();

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

        var finalLabel = result.FusionPrediction?.Label;
        var finalConfidence = result.FusionPrediction?.Confidence ?? 0;

        ultrasoundResult.ImageUrl = request.OriginalUrl;
        ultrasoundResult.MaskUrl = request.PredMaskUrl;
        ultrasoundResult.Csa = request.CsaMm2;
        ultrasoundResult.Perimeter = request.Perimeter;
        ultrasoundResult.FlatteningRatio = request.FlatteningRatio;
        ultrasoundResult.Circularity = request.Circularity;
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
            csa = ultrasoundResult.Csa,
            perimeter = ultrasoundResult.Perimeter,
            flatteningRatio = ultrasoundResult.FlatteningRatio,
            circularity = ultrasoundResult.Circularity,
            label = finalLabel,
            confidence = finalConfidence,
            contourPoints = ultrasoundResult.ContourPoints,
            status = ultrasoundResult.Status,
            imagePrediction = result.ImagePrediction,
            featurePrediction = result.FeaturePrediction,
            fusionPrediction = result.FusionPrediction
        });
    }

    [HttpPost("cal_features")]
    public async Task<ActionResult> CalCsa([FromBody] CalCsaRequest request)
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
                message = "Cần ít nhất 3 điểm contour để tính CSA."
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
            pythonResponse = await client.PostAsJsonAsync("cal_features", new
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
                message = "Không gọi được API cal_features bên Python.",
                error = ex.Message
            });
        }

        var json = await pythonResponse.Content.ReadAsStringAsync();

        if (!pythonResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)pythonResponse.StatusCode, new
            {
                message = "Python API cal_features trả lỗi.",
                detail = json
            });
        }

        var calCsaResult = JsonSerializer.Deserialize<PythonCalCsaResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        if (calCsaResult == null)
        {
            return StatusCode(500, new
            {
                message = "Không parse được response từ Python cal_features.",
                raw = json
            });
        }

        ultrasoundResult.Csa = calCsaResult.CsaMm2;
        ultrasoundResult.Perimeter = calCsaResult.Perimeter;
        ultrasoundResult.FlatteningRatio = calCsaResult.FlatteningRatio;
        ultrasoundResult.Circularity = calCsaResult.Circularity;
        ultrasoundResult.MaskUrl = calCsaResult.PredMaskUrl;
        ultrasoundResult.ContourPoints  = request.Contours;
        // ultrasoundResult.Status = "SEGMENTED";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            // success = true,
            // ultrasoundResultId = ultrasoundResult.Id,
            csaMm2 = calCsaResult.CsaMm2,
            perimeter = calCsaResult.Perimeter,
            flatteningRatio = calCsaResult.FlatteningRatio,
            circularity = calCsaResult.Circularity,
            pred_mask_url = calCsaResult.PredMaskUrl,
            contourPoints = request.Contours,
            // areaPx = calCsaResult.AreaPx,
            // status = ultrasoundResult.Status
        });
    }

    [HttpPost("confirm")]
    public async Task<ActionResult> Confirm([FromForm] int ultrasoundResultId, [FromForm] String status)
    {
        var ultrasound = await _context.UltrasoundResults.FirstOrDefaultAsync(u => u.Id == ultrasoundResultId);
        if (ultrasound != null)
        {
            ultrasound.Status = status;
            await _context.SaveChangesAsync();
            return Ok("ok");
        }
        if (ultrasound == null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy kết quả siêu âm."
            });
        }
        ;
        ultrasound.Status = status;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "ok."
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
            ultrasound.Csa,
            ultrasound.Circularity,
            ultrasound.FlatteningRatio,
            ultrasound.Perimeter,
            ultrasound.Label,
            ultrasound.Confidence,
            ultrasound.Status,
            ultrasound.ContourPoints
        });
    }
}

