using CTS_backend.Data;
using CTS_backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClinicalRecordControler : ControllerBase
{
    private readonly AppDbContext _context;

    public ClinicalRecordControler(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("getPatients")]
    public async Task<ActionResult> GetPatients([FromQuery] int userId)
    {
        var rawData = await (
            from c in _context.ClinicalRecord
            join p in _context.Patients on c.PatientId equals p.Id
            join s in _context.Staffs on c.DoctorId equals s.Id
            where s.UserId == userId
            select new
            {
                p.Id,
                p.Name,
                p.DateBirth,
                p.Gender,
                c.Time
            }
        )
        .Distinct()
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

        // DB của bạn đang dạng: 02/01/2004
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