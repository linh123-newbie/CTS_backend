using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models;

public class Users
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("google_id")]
    public String GoogleId { get; set; } = string.Empty;
    [Column("email")]
    public String Email { get; set; } = string.Empty;
    [Column("name")]
    public String Name { get; set; } = string.Empty;
    [Column("role_id")]
    public int RoleId { get; set; }
    [ForeignKey(nameof(RoleId))]
    public Roles? Role { get; set; }

    public ICollection<SelfAssessment>? SelfAssessments { get; set; }
}