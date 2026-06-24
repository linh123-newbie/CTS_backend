using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CTS_backend.Models.DTOs;

namespace CTS_backend.Models;

public class NcsFeatures
{
    //hello
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    [Column("name")]
    public string? Name { get; set; }
    [Column("unit")]
    public string? Unit { get; set; }
    
    
}