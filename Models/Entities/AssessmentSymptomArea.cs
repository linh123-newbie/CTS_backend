using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class AssessmentSymptomArea
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("self_assessment_id")]
    public int SelfAssessmentId { get; set; }
    [Column("hand")]
    public int Hand { get; set; }
    [Column("painful_place")]
    public String PainfulPlace { get; set; } = string.Empty;
    [Column("symptom")]
    public String Symptom { get; set; } = string.Empty;
    public SelfAssessment? SelfAssessment { get; set; }
}