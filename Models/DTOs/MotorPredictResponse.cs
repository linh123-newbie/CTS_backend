using System.Text.Json.Serialization;

namespace CTS_backend.Models.DTOs;

public class MotorPredictResponse
{
    [JsonPropertyName("pred")]
    public List<string>? Pred { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("features")]
    public MotorFeatures? Features { get; set; }

    [JsonPropertyName("probabilities")]
    public MotorProbabilities? Probabilities { get; set; }
}

public class MotorProbabilities
{
    [JsonPropertyName("bt")]
    public double Bt { get; set; }

    [JsonPropertyName("nang")]
    public double Nang { get; set; }

    [JsonPropertyName("nhe")]
    public double Nhe { get; set; }

    [JsonPropertyName("tb")]
    public double Tb { get; set; }
}