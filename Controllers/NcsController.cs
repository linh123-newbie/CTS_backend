using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CTS_backend.Models.DTOs;
using System.Net.Http.Headers;
using System.Text.Json;
using Amazon.S3;
using System.Globalization;
using Amazon.S3.Model;
using CTS_backend.Models;


namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NcsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly HttpClient _waveformClient;
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;

    public NcsController(AppDbContext context, IHttpClientFactory httpClientFactory, IAmazonS3 s3Client, IConfiguration configuration)
    {
        _context = context;
        _waveformClient = httpClientFactory.CreateClient("WaveformAi");
        _s3Client = s3Client;
        _configuration = configuration;
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
    [HttpGet("getSignalResults")]
    public async Task<ActionResult> GetSignalResults([FromQuery] int ncsResultId, [FromQuery] string measurementType)
    {
        var data = await _context.NcsNerveDetails
            .Where(n => n.NcsResultId == ncsResultId && n.MeasurementType == measurementType)
            .OrderByDescending(n => n.Id)
            .Select(n => new
            {
                id = n.Id,
                label = n.AiLabel,
                confidence = n.AiConfidence,
            })
            .ToListAsync();

        return Ok(data);
    }

    [HttpPost("features")]
    public async Task<IActionResult> GetNcsFeatures(double distance, IFormFile file)
    {
        if (distance <= 0)
        {
            return BadRequest("Khoảng cách không hợp lệ.");
        }
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
        var distanceText = distance.ToString(CultureInfo.InvariantCulture);

        var response = await _waveformClient.PostAsync(
        $"features?distance={Uri.EscapeDataString(distanceText)}",
        formData
    );

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
        if (result == null)
        {
            return StatusCode(500, "Không đọc được kết quả.");
        }
        result.Distance = distance;

        return Ok(result);
    }
    [HttpPost("calculate_features")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CalculateFeatures([FromQuery] double distance, [FromForm] double onset_x, [FromForm] double peak_x, IFormFile file)
    {
        if (distance <= 0)
        {
            return BadRequest("Khoảng cách không hợp lệ.");
        }
        if (peak_x < onset_x)
        {
            return BadRequest("Peak phải lớn hơn onset.");
        }

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
        var markersJson = JsonSerializer.Serialize(new
        {
            onset_x,
            peak_x
        });

        formData.Add(new StringContent(markersJson), "markers");
        var distanceText = distance.ToString(CultureInfo.InvariantCulture);

        var response = await _waveformClient.PostAsync(
       $"calculate_features?distance={Uri.EscapeDataString(distanceText)}",
       formData
   );

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
        if (result == null)
        {
            return StatusCode(500, "Không đọc được kết quả.");
        }
        result.Distance = distance;

        return Ok(result);
    }
    [HttpPost("calculate_motor_features")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CalculateMotorFeatures([FromForm] double onset_x1, [FromForm] double peak_x1, [FromForm] double onset_x2, [FromForm] double peak_x2, IFormFile file1, IFormFile file2)
    {

        if (peak_x1 < onset_x1 || peak_x2 < onset_x2)
        {
            return BadRequest("Peak phải lớn hơn onset.");
        }


        if (file1 == null || file1.Length == 0)
        {
            return BadRequest("Vui lòng chọn file txt.");
        }
        if (file2 == null || file2.Length == 0)
        {
            return BadRequest("Vui lòng chọn file txt.");
        }

        var extension1 = Path.GetExtension(file1.FileName).ToLower();
        var extension2 = Path.GetExtension(file2.FileName).ToLower();

        if (extension1 != ".txt" || extension2 != ".txt")
        {
            return BadRequest("Chỉ cho phép upload file .txt.");
        }

        using var formData = new MultipartFormDataContent();

        await using var fileStream1 = file1.OpenReadStream();
        await using var fileStream2 = file2.OpenReadStream();
        using var fileContent1 = new StreamContent(fileStream1);
        using var fileContent2 = new StreamContent(fileStream2);

        fileContent1.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        fileContent2.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        formData.Add(fileContent1, "file1", file1.FileName);
        formData.Add(fileContent2, "file2", file2.FileName);
        var markersJson1 = JsonSerializer.Serialize(new
        {
            onset_x = onset_x1,
            peak_x = peak_x1
        });
        var markersJson2 = JsonSerializer.Serialize(new
        {
            onset_x = onset_x2,
            peak_x = peak_x2
        });

        formData.Add(new StringContent(markersJson1), "markers1");
        formData.Add(new StringContent(markersJson2), "markers2");

        var response = await _waveformClient.PostAsync($"calculate_motor_features", formData);

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, responseBody);
        }

        var result = JsonSerializer.Deserialize<NcsMotorFeatureResponse>(
            responseBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );
        if (result == null)
        {
            return StatusCode(500, "Không đọc được kết quả.");
        }
        return Ok(result);
    }

    [HttpPost("predict")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PredictNcs([FromForm] NcsPredictRequest request)
    {
        if (request.File == null || request.File.Length == 0)
            return BadRequest("Vui lòng chọn file.");

        var extension = Path.GetExtension(request.File.FileName).ToLower();

        if (extension != ".txt")
            return BadRequest("Chỉ cho phép upload file txt.");

        var featuresJson = request.FeaturesJson;

        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            var form = await Request.ReadFormAsync();

            var featureDict = new Dictionary<string, object?>();

            foreach (var item in form)
            {
                if (
                    item.Key.Equals("file", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("type", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("featuresJson", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                var value = item.Value.ToString();

                if (double.TryParse(
                        value,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number))
                {
                    featureDict[item.Key] = number;
                }
                else
                {
                    featureDict[item.Key] = value;
                }
            }

            if (featureDict.Count > 0)
            {
                featuresJson = JsonSerializer.Serialize(featureDict);
            }
        }

        if (string.IsNullOrWhiteSpace(featuresJson))
            return BadRequest("Thiếu đặc trưng dẫn truyền.");

        var features = JsonSerializer.Deserialize<NcsFeatures>(
            featuresJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        if (features == null)
            return BadRequest("Features không hợp lệ.");

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

        var bucketName = _configuration["AWS:BucketName"];

        if (string.IsNullOrWhiteSpace(bucketName))
            return StatusCode(500, "Chưa cấu hình AWS BucketName.");

        var folder = request.Type?.ToLower() == "motor"
            ? "motor"
            : "sensory";

        var safeFileName = Path.GetFileName(request.File.FileName);
        var s3Key = $"{folder}/{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}_{safeFileName}";

        await using var stream = request.File.OpenReadStream();

        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = s3Key,
            InputStream = stream,
            ContentType = "text/plain"
        };
        await _s3Client.PutObjectAsync(putRequest);
        var aiLabel = predictResult?.Pred != null && predictResult.Pred.Count > 0
    ? predictResult.Pred[0]
    : null;
        var ncsNerveDetail = new NcsNerveDetail
        {
            NcsResultId = request.NcsResultId,
            MeasurementType = request.Type,
            AiLabel = aiLabel,
            AiConfidence = predictResult?.Confidence,
            NerveType = request.NerveType,
            FingerIndex = request.FingerIndex
        };

        _context.NcsNerveDetails.Add(ncsNerveDetail);
        await _context.SaveChangesAsync();

        var signalFile = new NcsSignalFile
        {
            NcsNerveDetailId = ncsNerveDetail.Id,
            Site = request.Type,
            FilePath = s3Key
        };

        _context.NcsSignalFiles.Add(signalFile);
        await _context.SaveChangesAsync();


        return Ok(new
        {
            prediction = predictResult,
            fileName = safeFileName,
            ncsNerveDetailId = ncsNerveDetail.Id,
            signalFileId = signalFile.Id,
            filePath = s3Key
            // s3Key
        });
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

    [HttpPost("motor_predict")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PredictMotorNcs([FromForm] MotorPredictRequest request)
    {
        if (request.File1 == null || request.File1.Length == 0)
            return BadRequest("Vui lòng chọn file kích thích cổ tay.");

        if (request.File2 == null || request.File2.Length == 0)
            return BadRequest("Vui lòng chọn file kích thích khuỷu tay.");

        if (Path.GetExtension(request.File1.FileName).ToLower() != ".txt" ||
            Path.GetExtension(request.File2.FileName).ToLower() != ".txt")
        {
            return BadRequest("Chỉ cho phép upload file txt.");
        }

        var featuresJson = request.FeaturesJson;

        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            var form = await Request.ReadFormAsync();

            var featureDict = new Dictionary<string, object?>();

            foreach (var item in form)
            {
                if (
                    item.Key.Equals("file1", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("file2", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("featuresJson", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("ncsResultId", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("nerveType", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("fingerIndex", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                var value = item.Value.ToString();

                if (double.TryParse(
                        value,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number))
                {
                    featureDict[item.Key] = number;
                }
                else
                {
                    featureDict[item.Key] = value;
                }
            }

            if (featureDict.Count > 0)
            {
                featuresJson = JsonSerializer.Serialize(featureDict);
            }
        }

        if (string.IsNullOrWhiteSpace(featuresJson))
            return BadRequest("Thiếu đặc trưng vận động.");

        var features = JsonSerializer.Deserialize<MotorFeatures>(
            featuresJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );

        if (features == null)
        {
            return BadRequest("Đặc trưng vận động không hợp lệ.");
        }

        var predictResponse = await _waveformClient.PostAsJsonAsync(
            "motor_predict",
            features
        );

        var predictBody = await predictResponse.Content.ReadAsStringAsync();

        if (!predictResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)predictResponse.StatusCode, predictBody);
        }

        var predictResult = JsonSerializer.Deserialize<MotorPredictResponse>(
            predictBody,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }
        );
        var bucketName = _configuration["AWS:BucketName"];
        if (string.IsNullOrWhiteSpace(bucketName))
            return StatusCode(500, "Chưa cấu hình AWS BucketName.");

        var safeFileName1 = Path.GetFileName(request.File1.FileName);
        var safeFileName2 = Path.GetFileName(request.File2.FileName);

        var wristS3Key =
            $"motor/{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}_wrist_{safeFileName1}";

        var elbowS3Key =
            $"motor/{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid()}_elbow_{safeFileName2}";

        await using var wristStream = request.File1.OpenReadStream();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = wristS3Key,
            InputStream = wristStream,
            ContentType = "text/plain"
        });

        await using var elbowStream = request.File2.OpenReadStream();
        await _s3Client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = elbowS3Key,
            InputStream = elbowStream,
            ContentType = "text/plain"
        });

        var aiLabel = predictResult?.Pred != null && predictResult.Pred.Count > 0
            ? predictResult.Pred[0]
            : null;
        var ncsNerveDetail = new NcsNerveDetail
        {
            NcsResultId = request.NcsResultId,
            MeasurementType = "motor",
            AiLabel = aiLabel,
            AiConfidence = predictResult?.Confidence,
            NerveType = request.NerveType,
            FingerIndex = request.FingerIndex
        };

        _context.NcsNerveDetails.Add(ncsNerveDetail);
        await _context.SaveChangesAsync();

        var wristSignalFile = new NcsSignalFile
        {
            NcsNerveDetailId = ncsNerveDetail.Id,
            Site = "wrist",
            FilePath = wristS3Key
        };

        var elbowSignalFile = new NcsSignalFile
        {
            NcsNerveDetailId = ncsNerveDetail.Id,
            Site = "elbow",
            FilePath = elbowS3Key
        };

        _context.NcsSignalFiles.AddRange(wristSignalFile, elbowSignalFile);
        await _context.SaveChangesAsync();


        return Ok(predictResult);
    }
}