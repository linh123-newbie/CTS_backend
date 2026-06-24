public class SaveNcsNerveValuesRequest
{
    public int NcsNerveDetailId { get; set; }
    public List<NcsNerveValueRequest> Values { get; set; } = new();
}

public class NcsNerveValueRequest
{
    public int NcsFeatureId { get; set; }
    public decimal Value { get; set; }
}