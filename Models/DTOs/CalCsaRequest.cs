using System.Net.Http.Json;
using System.Text.Json.Serialization;
public class CalCsaRequest
{
    [JsonPropertyName("ultrasoundResultId")]
    public int UltrasoundResultId { get; set; }
    [JsonPropertyName("originalUrl")]
    public string? OriginalUrl { get; set; }

    [JsonPropertyName("contours")]
    public List<ContourPoints> Contours { get; set; } = new();
}


public class PythonCalCsaResponse
{
    [JsonPropertyName("csa_mm2")]
    public double CsaMm2 { get; set; }
    [JsonPropertyName("perimeter")]
    public double Perimeter { get; set; }
    [JsonPropertyName("flattening_ratio")]
    public double FlatteningRatio { get; set; }
    [JsonPropertyName("circularity")]
    public double Circularity { get; set; }

    [JsonPropertyName("pred_mask_url")]
    public string? PredMaskUrl { get; set; }
}