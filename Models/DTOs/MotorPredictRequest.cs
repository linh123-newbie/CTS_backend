public class MotorPredictRequest
{
    public IFormFile? File1 { get; set; } // wrist
    public IFormFile? File2 { get; set; } // elbow

    public string? FeaturesJson { get; set; }

    public int NcsResultId { get; set; }

    public int? NerveType { get; set; }

    // Nếu chưa có muscle_index thì tạm để 0 hoặc null
    public int? FingerIndex { get; set; }
}