using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class ClinicalRecord
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("patient_id")]
    public int PatientId { get; set; }
    [Column("doctor_id")]
    public int DoctorId { get; set; }
    [Column("time", TypeName = "timestamp without time zone")]
    public DateTime Time { get; set; }
    [Column("result")]
    public String Result { get; set; } = string.Empty;
   
    [ForeignKey(nameof(PatientId))]
    public Patients? Patient { get; set; }
    [ForeignKey(nameof(DoctorId))]
    public Staffs? Staff { get; set; }

}