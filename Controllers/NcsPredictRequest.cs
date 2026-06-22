using Microsoft.AspNetCore.Mvc;

namespace CTS_backend.Models.DTOs;

public class NcsPredictRequest
{
    public IFormFile? File { get; set; }

    public string? Type { get; set; }

    public string? FeaturesJson { get; set; }
}