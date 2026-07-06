using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CTS_backend.Models.DTOs;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Globalization;
using CTS_backend.Models;
using System.Text;
using Amazon.S3.Model;
using Npgsql.EntityFrameworkCore.PostgreSQL.ValueGeneration.Internal;
using Npgsql.Internal;


namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NcsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly HttpClient _waveformClient;
    private readonly IHttpClientFactory _httpClientFactory;


    public NcsController(AppDbContext context, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _waveformClient = httpClientFactory.CreateClient("WaveformAi");
    }

    private async Task<List<double>> ReadSignalValuesFromUrlAsync(string? signalUrl)
{
    if (string.IsNullOrWhiteSpace(signalUrl))
    {
        return new List<double>();
    }

    if (!Uri.TryCreate(signalUrl, UriKind.Absolute, out var uri))
    {
        return new List<double>();
    }

    if (
        uri.Scheme != "https" ||
        uri.Host != "txt-signals.s3.ap-southeast-2.amazonaws.com" ||
        !uri.AbsolutePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
    )
    {
        return new List<double>();
    }

    var httpClient = _httpClientFactory.CreateClient();

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    var text = await httpClient.GetStringAsync(uri, cts.Token);

    var values = new List<double>();

    foreach (var part in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
    {
        if (
            double.TryParse(
                part,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var value
            ) &&
            double.IsFinite(value)
        )
        {
            values.Add(value);
        }
    }

    return values;
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

    [HttpGet("getDetailNerve")]
    public async Task<ActionResult> GetNerveDetail([FromQuery] int ncsNerveDetailId)
    {

        if (ncsNerveDetailId <= 0)
        {
            return BadRequest("Missing Id.");
        }

        var label = await _context.NcsNerveDetails
            .Where(n => n.Id == ncsNerveDetailId)
            .Select(n => new
            {
                id = n.Id,
                label = n.AiLabel,
                confidence = n.AiConfidence,
                Confirm = n.Confirm,

            })
            .ToListAsync();

        var signals = await _context.NcsNerveDetails
            .Where(n => n.Id == ncsNerveDetailId)
            .Join(_context.NcsSignalFiles, nn => nn.Id, ns => ns.NcsNerveDetailId, (nn, ns) => new
            {
                file_path = ns.FilePath
            }).ToListAsync();

        var features = from nn in _context.NcsNerveDetails
                       join nsv in _context.NcsNerveValues on nn.Id equals nsv.NcsNerveDetailId
                       join feature in _context.NcsFeatures on nsv.NcsFeatureId equals feature.Id
                       where nn.Id == ncsNerveDetailId
                       select new
                       {
                           Name = feature.Name,
                           Value = nsv.Value,
                           Unit = feature.Unit
                       };
        var data = new
        {
            label,
            signals,
            features
        };

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
    public async Task<IActionResult> GetNcsFeatures([FromForm] NcsRequest request)
    {
        if (!request.Distance.HasValue || request.Distance.Value <= 0)
        {
            return BadRequest("Khoảng cách không hợp lệ.");
        }
        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest("Vui lòng chọn file ảnh.");
        }

        var extension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();

        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
        {
            return BadRequest("Chỉ cho phép upload ảnh PNG/JPG.");
        }

        using var formData = new MultipartFormDataContent();

        await using var fileStream = request.Image.OpenReadStream();
        using var fileContent = new StreamContent(fileStream);

        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.Image.ContentType);

        formData.Add(fileContent, "file", request.Image.FileName);
        var distanceText = request.Distance.Value.ToString(CultureInfo.InvariantCulture);

        var response = await _waveformClient.PostAsync(
        $"input/sensory_features?distance={Uri.EscapeDataString(distanceText)}",
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
        var ncsNerveDetail = new NcsNerveDetail
        {
            NcsResultId = request.NcsResultId,
            MeasurementType = "sensory",
            NerveType = request.NerveType,
            FingerIndex = request.FingerIndex
        };
        _context.NcsNerveDetails.Add(ncsNerveDetail);
        await _context.SaveChangesAsync();

        var ncsSignalFile = new NcsSignalFile
        {
            Site = "sensory",
            FilePath = result.ScaledSignal,
            NcsNerveDetailId = ncsNerveDetail.Id,
        };
        _context.NcsSignalFiles.Add(ncsSignalFile);
        await _context.SaveChangesAsync();

        result.NcsNerveDetailId = ncsNerveDetail.Id;
        result.SignalValues = await ReadSignalValuesFromUrlAsync(result.ScaledSignal);


        return Ok(result);
    }
    [HttpPost("calculate_features")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CalculateFeatures([FromQuery] double distance, [FromForm] double onset_x, [FromForm] double peak_x, string scaled_signal_url)
    {
        if (distance <= 0)
        {
            return BadRequest("Khoảng cách không hợp lệ.");
        }
        if (peak_x < onset_x)
        {
            return BadRequest("Peak phải lớn hơn onset.");
        }

        if (string.IsNullOrWhiteSpace(scaled_signal_url))
        {
            return BadRequest("Lỗi đọc file txt.");
        }

        var extension = Path.GetExtension(scaled_signal_url).ToLower();

        if (extension != ".txt")
        {
            return BadRequest("Phải trỏ tới file txt.");
        }

        using var formData = new MultipartFormDataContent();

        var markersJson = JsonSerializer.Serialize(new
        {
            onset_x,
            peak_x
        });
        formData.Add(new StringContent(markersJson, Encoding.UTF8, "application/json"), "markers");
        formData.Add(new StringContent(scaled_signal_url, Encoding.UTF8), "scaled_signal_url");
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
    public async Task<IActionResult> CalculateMotorFeatures([FromForm] double onset_x1, [FromForm] double peak_x1, [FromForm] double onset_x2, [FromForm] double peak_x2, [FromForm] String scaled_signal_url1, [FromForm] String scaled_signal_url2)
    {

        if (peak_x1 < onset_x1 || peak_x2 < onset_x2)
        {
            return BadRequest("Peak phải lớn hơn onset.");
        }


        if (string.IsNullOrWhiteSpace(scaled_signal_url1) || string.IsNullOrWhiteSpace(scaled_signal_url2))
        {
            return BadRequest("Lỗi đọc file txt.");
        }

        var extension1 = Path.GetExtension(scaled_signal_url1).ToLower();
        var extension2 = Path.GetExtension(scaled_signal_url2).ToLower();

        if (extension1 != ".txt" || extension2 != ".txt")
        {
            return BadRequest("Chỉ cho phép upload file .txt.");
        }

        using var formData = new MultipartFormDataContent();


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
        formData.Add(new StringContent(scaled_signal_url1, Encoding.UTF8), "scaled_signal_url1");
        formData.Add(new StringContent(scaled_signal_url2, Encoding.UTF8), "scaled_signal_url2");

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

    private async Task<string?> UpdateNcsResultStatusIfBothPredictedAsync(int? ncsResultId)
    {
        if (ncsResultId == null || ncsResultId <= 0)
        {
            return null;
        }

        var ncsResult = await _context.NcsResults
            .FirstOrDefaultAsync(x => x.Id == ncsResultId);

        if (ncsResult == null)
        {
            return null;
        }

        var hasSensory = await _context.NcsNerveDetails
            .AnyAsync(x =>
                x.NcsResultId == ncsResultId &&
                x.MeasurementType != null &&
                x.MeasurementType.ToLower() == "sensory" &&
                x.AiLabel != null);

        var hasMotor = await _context.NcsNerveDetails
            .AnyAsync(x =>
                x.NcsResultId == ncsResultId &&
                x.MeasurementType != null &&
                x.MeasurementType.ToLower() == "motor" &&
                x.AiLabel != null);

        var hasSensoryDone = await _context.NcsNerveDetails
            .AnyAsync(x =>
                x.NcsResultId == ncsResultId &&
                x.MeasurementType != null &&
                x.MeasurementType.ToLower() == "sensory" &&
                x.Confirm == true);

        var hasMotorDone = await _context.NcsNerveDetails
            .AnyAsync(x =>
                x.NcsResultId == ncsResultId &&
                x.MeasurementType != null &&
                x.MeasurementType.ToLower() == "motor" &&
                x.Confirm == true);

        if (hasSensoryDone && hasMotorDone)
        {
            ncsResult.Status = "Đã xử lí";
        }
        else if (hasSensory && hasMotor)
        {
            ncsResult.Status = "CONFIRM";
        }
        else if (hasSensory || hasMotor)
        {
            ncsResult.Status = "PROCESSING";
        }
        else
        {
            ncsResult.Status = "Chưa xử lý";
        }

        await _context.SaveChangesAsync();

        return ncsResult.Status;
    }

    [HttpPost("predict")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PredictNcs([FromForm] NcsPredictRequest request)
    {
        if (!request.NcsResultId.HasValue || request.NcsResultId.Value <= 0)
        {
            return BadRequest("Thiếu mã kết quả NCS.");
        }

        if (!request.NcsNerveDetailId.HasValue || request.NcsNerveDetailId.Value <= 0)
        {
            return BadRequest("Thiếu ncsNerveDetailId.");
        }

        var featuresJson = request.FeaturesJson;

        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            var form = await Request.ReadFormAsync();

            var formFeatureDict = new Dictionary<string, object?>();

            foreach (var item in form)
            {
                if (
                    item.Key.Equals("ncsResultId", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("ncsNerveDetailId", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("featuresJson", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                var valueText = item.Value.ToString();

                if (double.TryParse(
                        valueText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number))
                {
                    formFeatureDict[item.Key] = number;
                }
                else
                {
                    formFeatureDict[item.Key] = valueText;
                }
            }

            if (formFeatureDict.Count > 0)
            {
                featuresJson = JsonSerializer.Serialize(formFeatureDict);
            }
        }

        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            return BadRequest("Thiếu đặc trưng dẫn truyền.");
        }
        var ncsNerveDetail = await _context.NcsNerveDetails
            .FirstOrDefaultAsync(x =>
                x.Id == request.NcsNerveDetailId.Value &&
                x.NcsResultId == request.NcsResultId.Value
            );

        if (ncsNerveDetail == null)
        {
            return NotFound("Không tìm thấy ncs_nerve_detail.");
        }

        var predictResponse = await _waveformClient.PostAsync(
            "predict",
            new StringContent(featuresJson, Encoding.UTF8, "application/json")
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

        var aiLabel = predictResult?.Pred != null && predictResult.Pred.Count > 0
            ? predictResult.Pred[0]
            : null;

        // Update detail cũ, không tạo detail mới
        ncsNerveDetail.AiLabel = aiLabel;
        ncsNerveDetail.AiConfidence = predictResult?.Confidence;

        await _context.SaveChangesAsync();

        Dictionary<string, JsonElement>? featureDict;

        try
        {
            featureDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                featuresJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
        }
        catch
        {
            return BadRequest("FeaturesJson không hợp lệ.");
        }

        if (featureDict == null || featureDict.Count == 0)
        {
            return BadRequest("FeaturesJson rỗng.");
        }

        var featureIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["onset_lat"] = 2,
            ["peak_lat (ms)"] = 3,
            ["hs (uV)"] = 4,
            ["rise_time (ms)"] = 5,
            ["as (uV.ms)"] = 6,
            ["asa (uV.ms)"] = 7,
            ["half_peak (ms)"] = 8,
            ["upper_lower"] = 9,
            ["left_right"] = 10,
            ["left_slope (uV/ms)"] = 11,
            ["right_slope (uV/ms)"] = 12,
            ["cv (m/s)"] = 14
        };

        var rows = new List<NcsNerveValue>();

        foreach (var item in featureDict)
        {
            if (!featureIdMap.TryGetValue(item.Key, out var featureId))
            {
                continue;
            }

            double value;

            if (item.Value.ValueKind == JsonValueKind.Number)
            {
                value = item.Value.GetDouble();
            }
            else if (
                item.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(
                    item.Value.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedValue
                )
            )
            {
                value = parsedValue;
            }
            else
            {
                continue;
            }

            if (!double.IsFinite(value))
            {
                continue;
            }

            rows.Add(new NcsNerveValue
            {
                NcsNerveDetailId = ncsNerveDetail.Id,
                NcsFeatureId = featureId,
                Value = value
            });
        }

        if (rows.Count > 0)
        {
            var oldValues = await _context.NcsNerveValues
                .Where(x => x.NcsNerveDetailId == ncsNerveDetail.Id)
                .ToListAsync();

            if (oldValues.Count > 0)
            {
                _context.NcsNerveValues.RemoveRange(oldValues);
                await _context.SaveChangesAsync();
            }

            _context.NcsNerveValues.AddRange(rows);
            await _context.SaveChangesAsync();
        }


        return Ok(new
        {
            prediction = predictResult,
            ncsNerveDetailId = ncsNerveDetail.Id,
            savedFeatureValueCount = rows.Count,
        });
    }

    [HttpPost("motor_features")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> GetMotorFeatures(NcsRequest request)
    {

        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest("Vui lòng chọn file ảnh.");
        }


        var extension = Path.GetExtension(request.Image.FileName).ToLowerInvariant();

        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
        {
            return BadRequest("Chỉ cho phép upload ảnh PNG/JPG.");
        }

        using var formData = new MultipartFormDataContent();

        await using var fileStream = request.Image.OpenReadStream();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.Image.ContentType);
        formData.Add(fileContent, "file", request.Image.FileName);

        var response = await _waveformClient.PostAsync("input/motor", formData);

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
        if (result == null)
        {
            return StatusCode(500, "Không đọc được kết quả.");
        }

        var ncsNerveDetail = new NcsNerveDetail
        {
            NcsResultId = request.NcsResultId,
            MeasurementType = "motor",
            NerveType = request.NerveType,
            FingerIndex = request.FingerIndex
        };
        _context.NcsNerveDetails.Add(ncsNerveDetail);
        await _context.SaveChangesAsync();

        var ncsSignalFile1 = new NcsSignalFile
        {
            Site = "wrist",
            FilePath = result.A1SignalUrl,
            NcsNerveDetailId = ncsNerveDetail.Id,
        };
        _context.NcsSignalFiles.Add(ncsSignalFile1);

        var ncsSignalFile2 = new NcsSignalFile
        {
            Site = "elbow",
            FilePath = result.A2SignalUrl,
            NcsNerveDetailId = ncsNerveDetail.Id,
        };
        _context.NcsSignalFiles.Add(ncsSignalFile2);
        await _context.SaveChangesAsync();

        result.NcsNerveDetailId = ncsNerveDetail.Id;

        return Ok(result);
    }

    [HttpPost("motor_predict")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PredictMotorNcs([FromForm] MotorPredictRequest request)
    {
        if (!request.NcsResultId.HasValue || request.NcsResultId.Value <= 0)
        {
            return BadRequest("Thiếu mã kết quả NCS.");
        }

        if (!request.NcsNerveDetailId.HasValue || request.NcsNerveDetailId.Value <= 0)
        {
            return BadRequest("Thiếu ncsNerveDetailId.");
        }

        var featuresJson = request.FeaturesJson;
        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            var form = await Request.ReadFormAsync();

            var formFeatureDict = new Dictionary<string, object?>();

            foreach (var item in form)
            {
                if (
                    item.Key.Equals("ncsResultId", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("ncsNerveDetailId", StringComparison.OrdinalIgnoreCase) ||
                    item.Key.Equals("featuresJson", StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }
                var valueText = item.Value.ToString();

                if (double.TryParse(
                        valueText,
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out var number))
                {
                    formFeatureDict[item.Key] = number;
                }
                else
                {
                    formFeatureDict[item.Key] = valueText;
                }

            }
            if (formFeatureDict.Count > 0)
            {
                featuresJson = JsonSerializer.Serialize(formFeatureDict);
            }

        }
        if (string.IsNullOrWhiteSpace(featuresJson))
        {
            return BadRequest("Thiếu đặc trưng dẫn truyền.");
        }
        var ncsNerveDetail = await _context.NcsNerveDetails
            .FirstOrDefaultAsync(x =>
                x.Id == request.NcsNerveDetailId.Value &&
                x.NcsResultId == request.NcsResultId.Value
            );

        if (ncsNerveDetail == null)
        {
            return NotFound("Không tìm thấy ncs_nerve_detail.");
        }

        var predictResponse = await _waveformClient.PostAsync(
            "motor_predict",
            new StringContent(featuresJson, Encoding.UTF8, "application/json")
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

        var aiLabel = predictResult?.Pred != null && predictResult.Pred.Count > 0
            ? predictResult.Pred[0]
            : null;

        ncsNerveDetail.AiLabel = aiLabel;
        ncsNerveDetail.AiConfidence = predictResult?.Confidence;

        await _context.SaveChangesAsync();

        Dictionary<string, JsonElement>? featureDict;
        try
        {
            featureDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                featuresJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );
        }
        catch
        {
            return BadRequest("FeaturesJson không hợp lệ.");
        }

        if (featureDict == null || featureDict.Count == 0)
        {
            return BadRequest("FeaturesJson rỗng.");
        }

        var featureIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["HmD"] = 16,
            ["HmP"] = 17,
            ["HmD_takeoff"] = 18,
            ["HmP_takeoff"] = 19,
            ["delta_takeoff"] = 20,
            ["w_peak_lat"] = 21,
            ["w_duration"] = 22,
            ["w_left_slope"] = 23,
            ["e_peak_lat"] = 24,
            ["e_duration"] = 25,
            ["e_left_slope"] = 26,
        };

        var rows = new List<NcsNerveValue>();

        foreach (var item in featureDict)
        {
            if (!featureIdMap.TryGetValue(item.Key, out var featureId))
            {
                continue;
            }
            double value;

            if (item.Value.ValueKind == JsonValueKind.Number)
            {
                value = item.Value.GetDouble();
            }
            else if (
                item.Value.ValueKind == JsonValueKind.String &&
                double.TryParse(
                    item.Value.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedValue
                )
            )
            {
                value = parsedValue;
            }
            else
            {
                continue;
            }
            if (!double.IsFinite(value))
            {
                continue;
            }

            rows.Add(new NcsNerveValue
            {
                NcsNerveDetailId = ncsNerveDetail.Id,
                NcsFeatureId = featureId,
                Value = value
            });


        }

        if (rows.Count > 0)
        {
            var oldValues = await _context.NcsNerveValues
                .Where(x => x.NcsNerveDetailId == ncsNerveDetail.Id)
                .ToListAsync();

            if (oldValues.Count > 0)
            {
                _context.NcsNerveValues.RemoveRange(oldValues);
                await _context.SaveChangesAsync();
            }

            _context.NcsNerveValues.AddRange(rows);
            await _context.SaveChangesAsync();
        }

        var ncsResultStatus = await UpdateNcsResultStatusIfBothPredictedAsync(request.NcsResultId);

        return Ok(new
        {
            prediction = predictResult,
            ncsNerveDetailId = ncsNerveDetail.Id,
            savedFeatureValueCount = rows.Count,
            status = ncsResultStatus
        });

    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromForm] int ncsNerveDetailId)
    {

        if (ncsNerveDetailId <= 0)
        {
            return BadRequest("missing nerve code.");
        }

        var ncsNerveDetail = await _context.NcsNerveDetails
       .FirstOrDefaultAsync(x => x.Id == ncsNerveDetailId);

        if (ncsNerveDetail == null)
        {
            return NotFound("nerve detail not found.");
        }

        ncsNerveDetail.Confirm = true;
        await _context.SaveChangesAsync();

        var ncsResultStatus = await UpdateNcsResultStatusIfBothPredictedAsync(ncsNerveDetail.NcsResultId);


        return Ok("confirm succesully");

    }



}