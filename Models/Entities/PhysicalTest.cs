using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class PhysicalTest
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("name")]
    public String Name { get; set; } = string.Empty;
    [Column("image_url")]
    public String ImageUrl { get; set; } = string.Empty;
    [Column("duration")]
    public int Duration { get; set; }
    [Column("description", TypeName = "json")]
    public JsonElement? Description { get; set; }
}