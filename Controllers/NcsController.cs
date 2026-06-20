using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CTS_backend.Models.DTOs;
using System.Net.Http.Headers;
using System.Text.Json;


namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NcsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly HttpClient _waveformClient;

    public NcsController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _waveformClient = httpClientFactory.CreateClient("WaveformAi");
    }

    [HttpGet("getNcsResults")]
    public async Task<ActionResult> GetNcsResults(
        [FromQuery] int doctorUserId)
    {
        var query =
            from n in _context.NcsResults
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

    [HttpPost("features")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetNcsFeatures(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Vui lòng chọn file txt.");
        }

        var extension = Path.GetExtension(file.FileName).ToLower();

        if (extension != ".txt")
        {
            return BadRequest("Chỉ cho phép upload file .txt.");
        }

        using var formData = new MultipartFormDataContent();

        await using var fileStream = file.OpenReadStream();
        using var fileContent = new StreamContent(fileStream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        formData.Add(fileContent, "file", file.FileName);

        var response = await _waveformClient.PostAsync("features", formData);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, responseBody);
        }

        var result = JsonSerializer.Deserialize<NcsFeatureResponse>(
            responseBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        return Ok(result);
    }

    [HttpPost("predict")]
    [Consumes("application/json")]
    public async Task<IActionResult> PredictNcs([FromBody] NcsFeatures features)
    {
        if (features == null)
        {
            return BadRequest("Vui lòng truyền features.");
        }

        var predictResponse = await _waveformClient.PostAsJsonAsync(
            "predict",
            features
        );

        var predictBody = await predictResponse.Content.ReadAsStringAsync();

        if (!predictResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)predictResponse.StatusCode, predictBody);
        }

        var predictResult = JsonSerializer.Deserialize<NcsPredictResponse>(
            predictBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        return Ok(predictResult);
    }

    [HttpPost("motor_features")]
[Consumes("multipart/form-data")]
public async Task<IActionResult> GetMotorFeatures(IFormFile file1, IFormFile file2)
{

    if (file1 == null || file1.Length == 0)
    {
        return BadRequest("Vui lòng chọn file A1.txt.");
    }

    if (file2 == null || file2.Length == 0)
    {
        return BadRequest("Vui lòng chọn file A2.txt.");
    }

    if (Path.GetExtension(file1.FileName).ToLower() != ".txt" ||
        Path.GetExtension(file2.FileName).ToLower() != ".txt")
    {
        return BadRequest("Chỉ cho phép upload file .txt.");
    }

    using var formData = new MultipartFormDataContent();

    await using var fileStream1 = file1.OpenReadStream();
    using var fileContent1 = new StreamContent(fileStream1);
    fileContent1.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
    formData.Add(fileContent1, "file1", file1.FileName);

    await using var fileStream2 = file2.OpenReadStream();
    using var fileContent2 = new StreamContent(fileStream2);
    fileContent2.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
    formData.Add(fileContent2, "file2", file2.FileName);

    var response = await _waveformClient.PostAsync("motor_features", formData);

    var responseBody = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        return StatusCode((int)response.StatusCode, responseBody);
    }

    var result = JsonSerializer.Deserialize<MotorFeatureResponse>(
        responseBody,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }
    );

    return Ok(result);
}
}