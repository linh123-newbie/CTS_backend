public class UltrasoundResultRequest
{
    public int UltrasoundResultId { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string PredMaskUrl { get; set; } = string.Empty;
    public double CsaMm2 { get; set; }
    public double Perimeter { get; set; }
    public double FlatteningRatio { get; set; }
    public double Circularity { get; set; }
    public List<ContourPoints> ContourPoints { get; set; } = [];
}