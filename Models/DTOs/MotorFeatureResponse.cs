using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class MotorFeatureResponse
{
    [JsonPropertyName("filename1")]
    public string? Filename1 { get; set; }

    [JsonPropertyName("filename2")]
    public string? Filename2 { get; set; }

    [JsonPropertyName("markers1")]
    public MotorMarker? Markers1 { get; set; }

    [JsonPropertyName("markers2")]
    public MotorMarker? Markers2 { get; set; }

    [JsonPropertyName("features")]
    public MotorFeatures? Features { get; set; }
}

public class MotorMarker
{
    [JsonPropertyName("peak_x")]
    public double PeakX { get; set; }

    [JsonPropertyName("peak_y")]
    public double PeakY { get; set; }

    [JsonPropertyName("onset_x")]
    public double OnsetX { get; set; }

    [JsonPropertyName("onset_y")]
    public double OnsetY { get; set; }
}

public class MotorFeatures
{
    [JsonPropertyName("HmD")]
    public double HmD { get; set; }

    [JsonPropertyName("HmP")]
    public double HmP { get; set; }

    [JsonPropertyName("HmD_takeoff")]
    public double HmDTakeoff { get; set; }

    [JsonPropertyName("HmP_takeoff")]
    public double HmPTakeoff { get; set; }
}