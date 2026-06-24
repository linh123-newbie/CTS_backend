namespace CTS_backend.Models.DTOs;

public class SaveNcsNerveValuesDto
{
    public int NcsNerveDetailId { get; set; }
    public List<NcsNerveValueItemDto> Values { get; set; } = new();
}

public class NcsNerveValueItemDto
{
    public int NcsFeatureId { get; set; }
    public double Value { get; set; }
}