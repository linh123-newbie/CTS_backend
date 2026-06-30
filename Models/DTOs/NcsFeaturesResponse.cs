using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class NcsFeatureResponse
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }
    [JsonPropertyName("scaled_signal_url")]
    public string? ScaledSignal { get; set; }

    [JsonPropertyName("markers")]
    public NcsMarkers? Markers { get; set; }

    [JsonPropertyName("features")]
    public NcsFeaturesDto? Features { get; set; }
    [JsonPropertyName("distance")]
    public double? Distance { get; set; }
}

public class NcsMarkers
{
    [JsonPropertyName("peak_x")]
    public double? PeakX { get; set; }

    [JsonPropertyName("peak_y")]
    public double? PeakY { get; set; }

    [JsonPropertyName("onset_x")]
    public double? OnsetX { get; set; }

    [JsonPropertyName("onset_y")]
    public double? OnsetY { get; set; }

    [JsonPropertyName("offset_x")]
    public double? OffsetX { get; set; }

    [JsonPropertyName("offset_y")]
    public double? OffsetY { get; set; }
}