using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PatientController : ControllerBase
{
    private readonly AppDbContext _context;

    public PatientController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("getPatients")]
    public async Task<ActionResult> GetPatients()
    {
        var rawData = await _context.Patients
            .AsNoTracking()
            .ToListAsync();

        var data = rawData.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            age = CalculateAge(p.DateBirth),
            gender = p.Gender == 1 ? "Nữ" : "Nam"
        });

        return Ok(data);
    }

    private int? CalculateAge(string dateBirth)
    {
        if (string.IsNullOrWhiteSpace(dateBirth))
            return null;

        if (!DateTime.TryParseExact(
                dateBirth,
                "dd/MM/yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime birthDate))
        {
            return null;
        }

        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;

        if (birthDate.Date > today.AddYears(-age))
            age--;

        return age;
    }
}