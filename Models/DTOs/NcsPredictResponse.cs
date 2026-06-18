using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class NcsPredictResponse
{
    [JsonPropertyName("pred")]
    public List<string> Pred { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("features")]
    public NcsFeatures? Features { get; set; }

    [JsonPropertyName("probabilities")]
    public Dictionary<string, double> Probabilities { get; set; } = new();
}