using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class Exercises
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("name")]
    public String Name { get; set; } = string.Empty;
    [Column("description")]
    public String Description { get; set; } = string.Empty;
    [Column("link")]
    public String Link { get; set; } = string.Empty;
    [Column("start_seconds")]
    public int StartSeconds { get; set; }
    [Column("end_seconds")]
    public int EndSeconds { get; set; }
    [Column("frequency")]
    public int Frequency { get; set; }
    [Column("level")]
    public String Level { get; set; } = string.Empty;
    
    
}