using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class MotorFeatureResponse
{
    [JsonPropertyName("filename")]
    public string? Filename1 { get; set; }

    [JsonPropertyName("markers1")]
    public MotorMarker? Markers1 { get; set; }

    [JsonPropertyName("markers2")]
    public MotorMarker? Markers2 { get; set; }
    [JsonPropertyName("a1_signal_url")]
    public string? A1SignalUrl { get; set; }
    [JsonPropertyName("a2_signal_url")]
    public string? A2SignalUrl { get; set; }

    [JsonPropertyName("features")]
    public MotorFeatures? Features { get; set; }
    [JsonPropertyName("ncsNerveDetail")]
    public int? NcsNerveDetailId { get; set; }
    [JsonPropertyName("a1_signal_values")]
    public List<double> A1SignalValues { get; set; } = new();

    [JsonPropertyName("a2_signal_values")]
    public List<double> A2SignalValues { get; set; } = new();
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

    [JsonPropertyName("cross_x")]
    public double CrossX { get; set; }

    [JsonPropertyName("cross_y")]
    public double CrossY { get; set; }
}

public class MotorFeatures
{
    [JsonPropertyName("HmD")]
    public double? HmD { get; set; }

    [JsonPropertyName("HmP")]
    public double? HmP { get; set; }

    [JsonPropertyName("HmD_takeoff")]
    public double? HmDTakeoff { get; set; }

    [JsonPropertyName("HmP_takeoff")]
    public double? HmPTakeoff { get; set; }

    [JsonPropertyName("delta_takeoff")]
    public double? DeltaTakeoff { get; set; }

    [JsonPropertyName("w_peak_lat")]
    public double? WPeakLat { get; set; }

    [JsonPropertyName("w_duration")]
    public double? WDuration { get; set; }

    [JsonPropertyName("w_left_slope")]
    public double? WLeftSlope { get; set; }

    [JsonPropertyName("e_peak_lat")]
    public double? EPeakLat { get; set; }

    [JsonPropertyName("e_duration")]
    public double? EDuration { get; set; }

    [JsonPropertyName("e_left_slope")]
    public double? ELeftSlope { get; set; }

    [JsonPropertyName("w_area")]
    public double? WArea { get; set; }

    [JsonPropertyName("e_area")]
    public double? EArea { get; set; }

    [JsonPropertyName("w_asa")]
    public double? WAsa { get; set; }

    [JsonPropertyName("e_asa")]
    public double? EAsa { get; set; }

    [JsonPropertyName("w_half_peak")]
    public double? WHalfPeak { get; set; }

    [JsonPropertyName("e_half_peak")]
    public double? EHalfPeak { get; set; }

    [JsonPropertyName("w_upper_ratio")]
    public double? WUpperRatio { get; set; }

    [JsonPropertyName("e_upper_ratio")]
    public double? EUpperRatio { get; set; }

    [JsonPropertyName("w_lower_ratio")]
    public double? WLowerRatio { get; set; }

    [JsonPropertyName("e_lower_ratio")]
    public double? ELowerRatio { get; set; }

    [JsonPropertyName("w_left_ratio")]
    public double? WLeftRatio { get; set; }

    [JsonPropertyName("e_left_ratio")]
    public double? ELeftRatio { get; set; }

    [JsonPropertyName("w_right_ratio")]
    public double? WRightRatio { get; set; }

    [JsonPropertyName("e_right_ratio")]
    public double? ERightRatio { get; set; }

    [JsonPropertyName("w_upper_lower")]
    public double? WUpperLower { get; set; }

    [JsonPropertyName("e_upper_lower")]
    public double? EUpperLower { get; set; }

    [JsonPropertyName("w_left_right")]
    public double? WLeftRight { get; set; }

    [JsonPropertyName("e_left_right")]
    public double? ELeftRight { get; set; }

    [JsonPropertyName("w_right_slope")]
    public double? WRightSlope { get; set; }

    [JsonPropertyName("e_right_slope")]
    public double? ERightSlope { get; set; }

    [JsonPropertyName("delta_Hm")]
    public double? DeltaHm { get; set; }

    [JsonPropertyName("delta_half_peak")]
    public double? DeltaHalfPeak { get; set; }
}