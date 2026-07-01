public class NcsPredictRequest
{
    public IFormFile? Image { get; set; } // sensory / motor
    public double? Distance { get; set; } // sensory / motor

    public int NcsResultId { get; set; }

    public int? NerveType { get; set; }

    public int? FingerIndex { get; set; }
}