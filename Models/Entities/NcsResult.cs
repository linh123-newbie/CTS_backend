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
    [Column("hand_result_id")]
    public int HandResultId { get; set; }
    [Column("label")]
    public String? Label { get; set; }
    [Column("confidence")]
    public double? Confidence { get; set; }
    [Column("status")]
    public String? Status { get; set; }
    [ForeignKey(nameof(HandResultId))]
    public HandResult? HandResult { get; set; }
    
    
}