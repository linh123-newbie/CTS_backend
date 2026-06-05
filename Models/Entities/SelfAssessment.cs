using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class SelfAssessment
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("time")]
    public DateTime  Time { get; set; }
    [Column("score")]
    public decimal? Score { get; set; }
    [Column("level")]
    public String? Level { get; set; } = string.Empty;

    // [ForeignKey(nameof(PatientId))]
    public Users? Users { get; set; }
    public ICollection<AssessmentAnswer>? AssessmentAnswers { get; set; }
    public ICollection<AssessmentPhysicalDetail>? AssessmentPhysicalDetails { get; set; }
    public ICollection<AssessmentSymptomArea>? AssessmentSymptomAreas { get; set; }
    
}