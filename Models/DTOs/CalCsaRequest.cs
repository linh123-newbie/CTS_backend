using System.Net.Http.Json;
using System.Text.Json.Serialization;
public class ContoursRequest
{
    [JsonPropertyName("ultrasoundResultId")]
    public int UltrasoundResultId { get; set; }
    [JsonPropertyName("originalUrl")]
    public string? OriginalUrl { get; set; }

    [JsonPropertyName("contours")]
    public List<ContourPoint> Contours { get; set; } = new();
}


public class ContoursResponse
{

    [JsonPropertyName("pred_mask_url")]
    public string? PredMaskUrl { get; set; }
}