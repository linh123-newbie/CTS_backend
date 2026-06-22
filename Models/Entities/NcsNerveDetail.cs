using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class NcsNerveDetail
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("ncs_result_id")]
    public int NcsResultId { get; set; }
    [Column("measurement_type")]
    public string? MeasurementType { get; set; }
    [Column("file_path")]
    public string? FilePath { get; set; }
    [Column("ai_label")]
    public string? AiLabel { get; set; }
    [Column("ai_confidence")]
    public double? AiConfidence { get; set; }
    [Column("nerve_type")]
    public int? NerveType { get; set; }
    [Column("finger_index")]
    public int? FingerIndex { get; set; }
    [ForeignKey(nameof(NcsResultId))]
    public NcsResult? NcsResult { get; set; }
    
    
}