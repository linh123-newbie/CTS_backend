using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class HandResult
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("hand")]
    public int? Hand { get; set; }
    [Column("label")]
    public string? Label { get; set; }
    [Column("confidence")]
    public double? Confidence { get; set; }
    [Column("note")]
    public string? Note { get; set; }
    [Column("clinical_record_id")]
    public int? ClinicalRecordId { get; set; }
    [Column("result")]
    public string? Result { get; set; }
    [ForeignKey(nameof(ClinicalRecordId))]
    public ClinicalRecord? ClinicalRecord { get; set; }
    
    
}