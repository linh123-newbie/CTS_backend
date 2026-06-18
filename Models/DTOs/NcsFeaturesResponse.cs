using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class NcsFeatureResponse
{
    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    [JsonPropertyName("markers")]
    public NcsMarkers? Markers { get; set; }

    [JsonPropertyName("features")]
    public NcsFeatures? Features { get; set; }
}

public class NcsMarkers
{
    [JsonPropertyName("peak_x")]
    public double PeakX { get; set; }

    [JsonPropertyName("peak_y")]
    public double PeakY { get; set; }

    [JsonPropertyName("onset_x")]
    public double OnsetX { get; set; }

    [JsonPropertyName("onset_y")]
    public double OnsetY { get; set; }

    [JsonPropertyName("offset_x")]
    public double OffsetX { get; set; }

    [JsonPropertyName("offset_y")]
    public double OffsetY { get; set; }
}