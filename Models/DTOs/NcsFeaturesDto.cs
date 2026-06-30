using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class NcsFeaturesDto
{
    [JsonPropertyName("hs (uV)")]
    public double Hs { get; set; }

    [JsonPropertyName("onset_lat")]
    public double OnsetLat { get; set; }

    [JsonPropertyName("peak_lat (ms)")]
    public double PeakLat { get; set; }

    [JsonPropertyName("rise_time (ms)")]
    public double RiseTime { get; set; }

    [JsonPropertyName("as (uV.ms)")]
    public double As { get; set; }
    [JsonPropertyName("asa (uV.ms)")]
    public double Asa { get; set; }
    [JsonPropertyName("half_peak (ms)")]
    public double HalfPeak { get; set; }
    [JsonPropertyName("upper_lower")]
    public double UpperLower { get; set; }
    [JsonPropertyName("left_right")]
    public double LeftRight { get; set; }
    [JsonPropertyName("left_slope (uV/ms)")]
    public double LeftSlope { get; set; }
    [JsonPropertyName("right_slope (uV/ms)")]
    public double RightSlope { get; set; }

    [JsonPropertyName("cv (m/s)")]
    public double Cv { get; set; }
}