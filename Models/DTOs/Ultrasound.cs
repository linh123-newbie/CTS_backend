using System.Text.Json.Serialization;

public class UltrasoundSegmentResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("original_url")]
    public string? OriginalUrl { get; set; }

    [JsonPropertyName("pred_mask_url")]
    public string? PredMaskUrl { get; set; }

    [JsonPropertyName("roi_url")]
    public string? RoiUrl { get; set; }

    [JsonPropertyName("marked_url")]
    public string? MarkedUrl { get; set; }

    [JsonPropertyName("csa_mm2")]
    public double? CsaMm2 { get; set; }

    // [JsonPropertyName("image_prediction")]
    // public ImagePrediction? ImagePrediction { get; set; }
}

public class ImagePrediction
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }
}