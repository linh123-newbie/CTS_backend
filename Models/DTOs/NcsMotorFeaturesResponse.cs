using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class NcsMotorFeatureResponse
{
    [JsonPropertyName("markers1")]
    public NcsMarkers? Markers1 { get; set; }
    [JsonPropertyName("markers2")]
    public NcsMarkers? Markers2 { get; set; }

    [JsonPropertyName("features")]
    public MotorFeatures? Features { get; set; }
   
}
