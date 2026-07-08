using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CTS_backend.Models.DTOs;

namespace CTS_backend.Models;

public class NcsReferenceRange
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("ncs_feature_id")]
    public int? NcsFeatureId { get; set; }
    [Column("normal_min")]
    public double? NormalMin { get; set; }
    [Column("normal_max")]
    public double? NormalMax { get; set; }
    [ForeignKey(nameof(NcsFeatureId))]
    public NcsFeatures? NcsFeatures { get; set; }
    
    
}