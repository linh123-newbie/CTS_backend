using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class AssessmentAnswer
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("self_assessment_id")]
    public int SelfAssessmentId { get; set; }
    [Column("bctq_answer_id")]
    public int BctqAnswerId { get; set; }
    public SelfAssessment? SelfAssessment { get; set; }
    public BctqAnswer? BctqAnswer { get; set; }
}