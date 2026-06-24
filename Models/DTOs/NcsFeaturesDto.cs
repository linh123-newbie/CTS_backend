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

    [JsonPropertyName("cv (m/s)")]
    public double Cv { get; set; }
}