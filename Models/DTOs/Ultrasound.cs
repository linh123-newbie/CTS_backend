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
    [JsonPropertyName("perimeter")]
    public double? Perimeter { get; set; }
    [JsonPropertyName("flattening_ratio")]
    public double? FlatteningRatio { get; set; }
    [JsonPropertyName("circularity")]
    public double? Circularity { get; set; }

    [JsonPropertyName("contour_points")]
    public List<ContourPoints>? ContourPoints { get; set; }

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

public class PythonUltrasoundResultResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("image_prediction")]
    public PredictionDto? ImagePrediction { get; set; }

    [JsonPropertyName("feature_prediction")]
    public PredictionDto? FeaturePrediction { get; set; }

    [JsonPropertyName("fusion_prediction")]
    public PredictionDto? FusionPrediction { get; set; }
}

public class PredictionDto
{
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("probabilities")]
    public Dictionary<string, double>? Probabilities { get; set; }
}