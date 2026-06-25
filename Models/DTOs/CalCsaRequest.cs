using System.Net.Http.Json;
using System.Text.Json.Serialization;
public class CalCsaRequest
{
    [JsonPropertyName("ultrasoundResultId")]
    public int UltrasoundResultId { get; set; }
    [JsonPropertyName("originalUrl")]
    public string? OriginalUrl { get; set; }

    [JsonPropertyName("contours")]
    public List<UltrasoundContourPointRequest> Contours { get; set; } = new();
}

public class UltrasoundContourPointRequest
{
    [JsonPropertyName("x")]
    public double X { get; set; }

    [JsonPropertyName("y")]
    public double Y { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

public class PythonCalCsaResponse
{
    [JsonPropertyName("csa_mm2")]
    public double CsaMm2 { get; set; }

    [JsonPropertyName("pred_mask_url")]
    public string? PredMaskUrl { get; set; }
}