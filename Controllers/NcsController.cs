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
using System.Data;
using System.Runtime.InteropServices;


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

        var signalFiles = await _context.NcsNerveDetails
    .Where(n => n.Id == ncsNerveDetailId)
    .Join(
        _context.NcsSignalFiles,
        nn => nn.Id,
        ns => ns.NcsNerveDetailId,
        (nn, ns) => new
        {
            site = ns.Site,
            file_path = ns.FilePath
        }
    )
    .ToListAsync();

        var signals = new List<object>();

        foreach (var signal in signalFiles
            .OrderBy(x =>
                x.site == "wrist" ? 0 :
                x.site == "elbow" ? 1 :
                x.site == "sensory" ? 0 :
                2
            ))
        {
            signals.Add(new
            {
                site = signal.site,
                file_path = signal.file_path,
                signal_values = await ReadSignalValuesFromUrlAsync(signal.file_path)
            });
        }

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

    private async Task<int> SaveSensoryFeatureValuesAsync(
    int ncsNerveDetailId,
    object? features
)
    {
        if (ncsNerveDetailId <= 0 || features == null)
        {
            return 0;
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

        Dictionary<string, JsonElement>? featureDict;

        try
        {
            var featuresJson = features is JsonElement jsonElement
                ? jsonElement.GetRawText()
                : JsonSerializer.Serialize(features);

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
            return 0;
        }

        if (featureDict == null || featureDict.Count == 0)
        {
            return 0;
        }

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
                NcsNerveDetailId = ncsNerveDetailId,
                NcsFeatureId = featureId,
                Value = value
            });
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var oldValues = await _context.NcsNerveValues
            .Where(x => x.NcsNerveDetailId == ncsNerveDetailId)
            .ToListAsync();

        if (oldValues.Count > 0)
        {
            _context.NcsNerveValues.RemoveRange(oldValues);
            await _context.SaveChangesAsync();
        }

        _context.NcsNerveValues.AddRange(rows);
        await _context.SaveChangesAsync();

        return rows.Count;
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
        await SaveSensoryFeatureValuesAsync(ncsNerveDetail.Id, result.Features);


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

    private class NcsSummaryResult
    {
        public string? Label { get; set; }
        public double? Confidence { get; set; }
    }

    private static int? GetNcsSeverityRank(string? label)
    {

        return label switch
        {
            "bt" => 0,
            "nhe" => 1,
            "tb" => 2,
            "nang" => 3,
            _ => null
        };
    }

    private static NcsSummaryResult BuildNcsSummary(
        string? sensoryLabel,
        double? sensoryConfidence,
        string? motorLabel,
        double? motorConfidence
    )
    {

        var sensoryRank = GetNcsSeverityRank(sensoryLabel);
        var motorRank = GetNcsSeverityRank(motorLabel);

        if (sensoryRank == null || motorRank == null)
        {
            return new NcsSummaryResult
            {
                Label = null,
                Confidence = null
            };
        }

        if (sensoryRank > motorRank)
        {
            return new NcsSummaryResult
            {
                Label = sensoryLabel,
                Confidence = sensoryConfidence
            };
        }

        if (motorRank > sensoryRank)
        {
            return new NcsSummaryResult
            {
                Label = motorLabel,
                Confidence = motorConfidence
            };
        }

        return new NcsSummaryResult
        {
            Label = sensoryLabel,
            Confidence =
                sensoryConfidence.HasValue && motorConfidence.HasValue
                    ? Math.Min(sensoryConfidence.Value, motorConfidence.Value)
                    : sensoryConfidence ?? motorConfidence
        };
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

        var details = await _context.NcsNerveDetails
            .Where(x =>
                x.NcsResultId == ncsResultId &&
                x.MeasurementType != null &&
                x.AiLabel != null)
            .ToListAsync();

        var confirmedSensory = details
            .Where(x =>
                x.Confirm == true &&
                x.MeasurementType!.ToLower() == "sensory")
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();

        var confirmedMotor = details
            .Where(x =>
                x.Confirm == true &&
                x.MeasurementType!.ToLower() == "motor")
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();

        var hasSensory = details.Any(x =>
            x.MeasurementType!.ToLower() == "sensory");

        var hasMotor = details.Any(x =>
            x.MeasurementType!.ToLower() == "motor");

        if (confirmedSensory != null && confirmedMotor != null)
        {
            var summary = BuildNcsSummary(
                confirmedSensory.AiLabel,
                confirmedSensory.AiConfidence,
                confirmedMotor.AiLabel,
                confirmedMotor.AiConfidence
            );

            ncsResult.Status = "Đã xử lý";
            ncsResult.Label = summary.Label;
            ncsResult.Confidence = summary.Confidence;
        }
        else if (hasSensory && hasMotor)
        {
            ncsResult.Status = "CONFIRM";
            ncsResult.Label = null;
            ncsResult.Confidence = null;
        }
        else if (hasSensory || hasMotor)
        {
            ncsResult.Status = "Đang xử lý";
            ncsResult.Label = null;
            ncsResult.Confidence = null;
        }
        else
        {
            ncsResult.Status = "Chưa xử lý";
            ncsResult.Label = null;
            ncsResult.Confidence = null;
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

    private async Task<int> SaveMotorFeatureValuesAsync(
    int ncsNerveDetailId,
    object? features
)
    {
        if (ncsNerveDetailId <= 0 || features == null)
        {
            return 0;
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

        Dictionary<string, JsonElement>? featureDict;

        try
        {
            var featuresJson = features is JsonElement jsonElement
                ? jsonElement.GetRawText()
                : JsonSerializer.Serialize(features);

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
            return 0;
        }

        if (featureDict == null || featureDict.Count == 0)
        {
            return 0;
        }

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
                NcsNerveDetailId = ncsNerveDetailId,
                NcsFeatureId = featureId,
                Value = value
            });
        }

        if (rows.Count == 0)
        {
            return 0;
        }

        var oldValues = await _context.NcsNerveValues
            .Where(x => x.NcsNerveDetailId == ncsNerveDetailId)
            .ToListAsync();

        if (oldValues.Count > 0)
        {
            _context.NcsNerveValues.RemoveRange(oldValues);
            await _context.SaveChangesAsync();
        }

        _context.NcsNerveValues.AddRange(rows);
        await _context.SaveChangesAsync();

        return rows.Count;
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

        result.A1SignalValues = await ReadSignalValuesFromUrlAsync(result.A1SignalUrl);
        result.A2SignalValues = await ReadSignalValuesFromUrlAsync(result.A2SignalUrl);
        await SaveMotorFeatureValuesAsync(ncsNerveDetail.Id, result.Features);

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


        return Ok(new
        {
            prediction = predictResult,
            ncsNerveDetailId = ncsNerveDetail.Id,
            savedFeatureValueCount = rows.Count,
        });

    }

    private static (string Status, string Evaluate) EvaluateNcsFeature(
    string? featureName,
    double? value,
    double? normalMin,
    double? normalMax,
    double? wristAmp,
    double? elbowAmp
)
    {
        if (value == null)
        {
            return (
                "Không có dữ liệu",
                "Chưa có giá trị thực tế để đánh giá."
            );
        }

        if (normalMin == null && normalMax == null)
        {
            return (
                "Chưa có ngưỡng",
                "Chưa có khoảng tham chiếu để đánh giá chỉ số này."
            );
        }

        var name = featureName?.Trim().ToLower() ?? "";

        var isSensoryAmplitude = name == "amplitude";
        var isSensoryLatency = name == "onset latency";

        var isMotorWristAmplitude = name == "hmd";
        var isMotorWristLatency = name == "hmd takeoff";

        var isMotorElbowAmplitude = name == "hmp";

        var isBelowMin = normalMin != null && value < normalMin;
        var isAboveMax = normalMax != null && value > normalMax;

        if (!isBelowMin && !isAboveMax)
        {
            if (
                isMotorElbowAmplitude &&
                wristAmp != null &&
                elbowAmp != null &&
                elbowAmp < wristAmp * 0.8
            )
            {
                return (
                    "Giảm",
                    "Biên độ khi kích thích tại khuỷu tay giảm hơn 20% so với cổ tay, nghẽn dẫn truyền đoạn giữa khuỷu tay và cổ tay."
                );
            }

            return (
                "Bình thường",
                ""
            );
        }

        if (isSensoryAmplitude && isBelowMin)
        {
            return (
                "Giảm",
                "Tổn thương trực tiếp vào các sợi trục cảm giác."
            );
        }

        if (isSensoryLatency && isAboveMax)
        {
            return (
                "Kéo dài",
                "Tổn thương bao myelin của sợi cảm giác, khiến tín hiệu truyền đi bị chậm lại."
            );
        }

        if (isMotorWristLatency && isAboveMax)
        {
            return (
                "Kéo dài",
                "Biểu hiện của tổn thương bao myelin đoạn xa."
            );
        }

        if (isMotorWristAmplitude && isBelowMin)
        {
            return (
                "Giảm",
                "Mất các sợi trục vận động hoặc nghẽn dẫn truyền tại cổ tay."
            );
        }

        if (isMotorElbowAmplitude && isBelowMin)
        {
            return (
                "Giảm",
                "Dòng điện bị nghẽn ở đoạn giữa khuỷu tay và cổ tay."
            );
        }

        if (isBelowMin)
        {
            return (
                "Giảm",
                "Giá trị thực tế thấp hơn ngưỡng chuẩn."
            );
        }

        return (
            "Kéo dài",
            "Giá trị thực tế cao hơn ngưỡng chuẩn."
        );
    }

    private static string FormatThreshold(double? normalMin, double? normalMax, string? unit)
    {
        var unitText = string.IsNullOrWhiteSpace(unit) ? "" : $" {unit}";

        if (normalMin != null && normalMax != null)
        {
            return $"{normalMin} - {normalMax}{unitText}";
        }

        if (normalMin != null)
        {
            return $">= {normalMin}{unitText}";
        }

        if (normalMax != null)
        {
            return $"<= {normalMax}{unitText}";
        }

        return "Chưa có ngưỡng";
    }

    [HttpGet("result")]
    public async Task<IActionResult> Result([FromQuery] int ncsResultId)
    {
        if (ncsResultId <= 0)
        {
            return BadRequest("Thiếu mã kết quả NCS.");
        }

        var patient = await (
            from n in _context.NcsResults
            join c in _context.ClinicalRecords on n.ClinicalRecordId equals c.Id
            join p in _context.Patients on c.PatientId equals p.Id
            where n.Id == ncsResultId
            select new
            {
                ncsResultId = n.Id,
                patientId = p.Id,
                patientName = p.Name,
                clinicalTime = c.Time,
                hand = n.Hand,
                dateBirth = p.DateBirth,

                ncsLabel = n.Label,
                ncsConfidence = n.Confidence,
                ncsStatus = n.Status
            }
        ).FirstOrDefaultAsync();

        if (patient == null)
        {
            return NotFound("Không tìm thấy kết quả NCS.");
        }

        if (patient.ncsStatus != "Đã xử lý")
        {
            return Conflict(new
            {
                message = "Cần xác nhận kết quả NCS cảm giác và NCS vận động trước khi xem kết quả cuối.",
                status = patient.ncsStatus
            });
        }

        var nerveDetails = await _context.NcsNerveDetails
            .Where(x => x.NcsResultId == ncsResultId && x.Confirm == true)
            .OrderBy(x => x.MeasurementType)
            .Select(x => new
            {
                id = x.Id,
                measurementType = x.MeasurementType,
                label = x.AiLabel,
                confidence = x.AiConfidence,
                confirm = x.Confirm
            })
            .ToListAsync();

        var rawReferenceValues = await (
    from nn in _context.NcsNerveValues
    join nf in _context.NcsFeatures on nn.NcsFeatureId equals nf.Id
    join nd in _context.NcsNerveDetails on nn.NcsNerveDetailId equals nd.Id
    join nr in _context.NcsReferenceRanges on nf.Id equals nr.NcsFeatureId
    where nd.NcsResultId == ncsResultId && nd.Confirm == true
          && nd.Confirm == true
          && new[] { 2, 4, 16, 17, 18 }.Contains(nf.Id)
    select new
    {
        featureId = nf.Id,
        name = nf.Name,
        unit = nf.Unit,
        value = nn.Value,
        normalMin = nr.NormalMin,
        normalMax = nr.NormalMax
    }
).ToListAsync();

        var wristAmp = rawReferenceValues
    .FirstOrDefault(x => x.name == "HmD")
    ?.value;

        var elbowAmp = rawReferenceValues
            .FirstOrDefault(x => x.name == "HmP")
            ?.value;

        var referenceValues = rawReferenceValues
            .Select(x =>
            {
                var result = EvaluateNcsFeature(
                    x.name,
                    x.value,
                    x.normalMin,
                    x.normalMax,
                    wristAmp,
                    elbowAmp
                );

                return new
                {
                    name = x.name,
                    unit = x.unit,
                    value = x.value,
                    threshold = FormatThreshold(x.normalMin, x.normalMax, x.unit),
                    status = result.Status,
                    evaluate = result.Evaluate
                };
            })
            .ToList();

        return Ok(new
        {
            patient,
            nerveDetails,
            referenceValues
        });
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromForm] int ncsNerveDetailId, [FromForm] bool confirm)
    {
        if (ncsNerveDetailId <= 0)
        {
            return BadRequest("Missing nerve detail id.");
        }

        var ncsNerveDetail = await _context.NcsNerveDetails
            .FirstOrDefaultAsync(x => x.Id == ncsNerveDetailId);

        if (ncsNerveDetail == null)
        {
            return NotFound("Nerve detail not found.");
        }

        if (string.IsNullOrWhiteSpace(ncsNerveDetail.MeasurementType))
        {
            return BadRequest("Missing measurement type.");
        }


        if (confirm)
        {
            var sameGroupDetails = await _context.NcsNerveDetails
                .Where(x =>
                    x.NcsResultId == ncsNerveDetail.NcsResultId &&
                    x.MeasurementType == ncsNerveDetail.MeasurementType)
                .ToListAsync();

            foreach (var detail in sameGroupDetails)
            {
                detail.Confirm = false;
            }

            ncsNerveDetail.Confirm = true;
        }
        else
        {
            ncsNerveDetail.Confirm = false;
        }

        await _context.SaveChangesAsync();

        var ncsResultStatus =
            await UpdateNcsResultStatusIfBothPredictedAsync(ncsNerveDetail.NcsResultId);

        return Ok("Confirm successfully.");
    }

    [HttpPost("ncs_result")]
    public async Task<IActionResult> NcsResult([FromForm] int ncsResultId, [FromForm] string type)
    {
        var files = await (
        from nr in _context.NcsResults.AsNoTracking()
        join nn in _context.NcsNerveDetails.AsNoTracking()
            on nr.Id equals nn.NcsResultId
        join ns in _context.NcsSignalFiles.AsNoTracking()
            on nn.Id equals ns.NcsNerveDetailId
        where nr.Id == ncsResultId && nn.Confirm == true
              && nn.MeasurementType == type
        select new
        {
            ns.FilePath
        }
    ).ToListAsync();

        var httpClient = _httpClientFactory.CreateClient();

        var waveform = new List<object>();

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.FilePath))
                continue;

            var text = await httpClient.GetStringAsync(file.FilePath);

            var signalValues = text
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => double.Parse(
                    x,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture
                ))
                .ToArray();

            waveform.Add(new
            {
                // site = file.Site,
                values = signalValues
            });
        }

        var values = await (
        from nr in _context.NcsResults.AsNoTracking()
        join nn in _context.NcsNerveDetails.AsNoTracking()
            on nr.Id equals nn.NcsResultId
        join nv in _context.NcsNerveValues.AsNoTracking()
            on nn.Id equals nv.NcsNerveDetailId
        join nf in _context.NcsFeatures.AsNoTracking()
            on nv.NcsFeatureId equals nf.Id
        where nr.Id == ncsResultId
              && nn.MeasurementType == type && nn.Confirm == true
        select new
        {
            nv.Value,
            nf.Name,
            nf.Unit
        }
    ).ToListAsync();
        return Ok(new
        {
            waveform,
            values
        });
    }



}