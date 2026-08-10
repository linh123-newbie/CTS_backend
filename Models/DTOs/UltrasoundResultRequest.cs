public class UltrasoundResultRequest
{
    public int UltrasoundResultId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string PredMaskUrl { get; set; } = string.Empty;
    public List<ContourPoint> ContourPoints { get; set; } = [];
}