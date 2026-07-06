using Microsoft.AspNetCore.Mvc;

namespace CTS_backend.Models.DTOs;

public class NcsPredictRequest
{
    public int? NcsResultId { get; set; }

    public int? NcsNerveDetailId { get; set; }

    public string? FeaturesJson { get; set; }
}