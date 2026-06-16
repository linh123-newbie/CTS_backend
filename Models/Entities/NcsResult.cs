using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class NcsResult
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("clinical_record_id")]
    public int ClinicalRecordId { get; set; }
    [Column("hand")]
    public int? Hand { get; set; }
    [Column("label")]
    public String? Label { get; set; }
    [Column("status")]
    public String? Status { get; set; }
    [ForeignKey(nameof(ClinicalRecordId))]
    public ClinicalRecord? ClinicalRecord { get; set; }
    
    
}