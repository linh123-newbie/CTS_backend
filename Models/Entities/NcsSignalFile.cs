using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class NcsSignalFile
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("ncs_nerve_detail_id")]
    public int NcsNerveDetailId { get; set; }
    [Column("site")]
    public String? Site { get; set; }
    [Column("file_path")]
    public String? FilePath { get; set; }
    
    [ForeignKey(nameof(NcsNerveDetailId))]
    public NcsNerveDetail? NcsNerveDetail { get; set; }
    
    
}