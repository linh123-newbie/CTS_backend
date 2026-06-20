using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CTS_backend.Models;

public class Staffs
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("user_id")]
    public int UserId { get; set; }
    [Column("name")]
    public String? Name { get; set; }
    [Column("phone")]
    public String? Phone { get; set; }
    [ForeignKey(nameof(UserId))]
    public Users? User { get; set; }
    
    
}