using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class AssessmentPhysicalDetail
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("self_assessment_id")]
    public int SelfAssessmentId { get; set; }
    [Column("physical_test_id")]
    public int PhysicalTestId { get; set; }
    [Column("is_positive")]
    public Boolean IsPositive { get; set; }
    public SelfAssessment? SelfAssessment { get; set; }
    public PhysicalTest? PhysicalTest { get; set; }
}