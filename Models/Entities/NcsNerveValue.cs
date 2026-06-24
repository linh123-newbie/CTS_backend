using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CTS_backend.Models.DTOs;

namespace CTS_backend.Models;

public class NcsNerveValue
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("ncs_nerve_detail_id")]
    public int? NcsNerveDetailId { get; set; }
    [Column("value")]
    public double? Value { get; set; }
    [Column("ncs_feature_id")]
    public int? NcsFeatureId { get; set; }
    [ForeignKey(nameof(NcsNerveDetailId))]
    public NcsNerveDetail? NcsNerveDetails { get; set; }
    [ForeignKey(nameof(NcsFeatureId))]
    public NcsFeatures? NcsFeatures { get; set; }
    
    
}