namespace CTS_backend.DTOs;

public class CreateSelfAssessmentRequest
{
    public List<SymptomAreaRequest> SymptomAreas { get; set; } = new();
    public List<int> BctqAnswerIds { get; set; } = new();
    public List<PhysicalDetailRequest> PhysicalDetails { get; set; } = new();
}

public class SymptomAreaRequest
{
    public int Hand { get; set; }
    public string PainfulPlace { get; set; } = string.Empty;
    public string Symptom { get; set; } = string.Empty;
}
public class PhysicalDetailRequest
{
    public int PhysicalTestId { get; set; }
    public bool IsPositive { get; set; }
}