using System.ComponentModel.DataAnnotations;

namespace CTS_backend.Models.DTOs;

public class NcsFeatureRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;
}