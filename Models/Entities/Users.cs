using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class Users
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int id { get; set; }
    [Column("google_id")]
    public String GoogleId { get; set; } = string.Empty;
    [Column("name")]
    public String Name { get; set; } = string.Empty;
    [Column("image_url")]
    public String ImageUrl { get; set; } = string.Empty;
}