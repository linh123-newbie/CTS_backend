using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class UltrasoundResult
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("image_url")]
    public String? ImageUrl { get; set; }
    [Column("mask_url")]
    public String? MaskUrl { get; set; }
    [Column("hand")]
    public int? Hand { get; set; }
    [Column("label")]
    public String? Label { get; set; }
    [Column("csa")]
    public double? Csa { get; set; }
    [Column("perimeter")]
    public double? Perimeter { get; set; }
    [Column("flattening_ratio")]
    public double? FlatteningRatio { get; set; }
    [Column("circularity")]
    public double? Circularity { get; set; }
    [Column("clinical_record_id")]
    public int ClinicalRecordId { get; set; }
    [Column("confidence")]
    public double? Confidence { get; set; }
    [Column("contour_points", TypeName = "jsonb")]
    public List<ContourPoints>? ContourPoints { get; set; }
    [Column("status")]
    public String? Status { get; set; }
    [ForeignKey(nameof(ClinicalRecordId))]
    public ClinicalRecord? ClinicalRecord { get; set; }
    
    
}