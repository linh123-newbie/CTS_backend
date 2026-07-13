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
    [Column("hand_result_id")]
    public int HandResultId { get; set; }
    [Column("confidence")]
    public double? Confidence { get; set; }
    [Column("contour_points", TypeName = "jsonb")]
    public List<ContourPoint>? ContourPoints { get; set; }
    [Column("status")]
    public String? Status { get; set; }
    [ForeignKey(nameof(HandResultId))]
    public HandResult? HandResult { get; set; }
    
    
}