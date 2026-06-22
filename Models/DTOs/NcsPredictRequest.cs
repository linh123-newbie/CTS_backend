public class NcsPredictRequest
{
    public IFormFile? File { get; set; }
    public string? Type { get; set; } // sensory / motor

    public string? FeaturesJson { get; set; }

    public int NcsResultId { get; set; }

    public int? NerveType { get; set; }

    public int? FingerIndex { get; set; }
}