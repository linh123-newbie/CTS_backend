using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class Patients
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("name")]
    public String Name { get; set; } = string.Empty;
    [Column("date_birth")]
    public String DateBirth { get; set; } = string.Empty;
    [Column("gender")]
    public int Gender { get; set; }
    [Column("phone")]
    public String Phone { get; set; } = string.Empty;
    [Column("weight")]
    public int Weight { get; set; }
    [Column("occupation")]
    public String Occupation { get; set; } = string.Empty;
    [Column("hand")]
    public int Hand { get; set; }

    
    
}