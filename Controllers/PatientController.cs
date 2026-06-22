using CTS_backend.Data;
using CTS_backend.Models;
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
    [HttpPut("addPatient")]
    public async Task<ActionResult> AddPatient([FromBody] Patients request)
    {
        if (request == null)
            return BadRequest("Dữ liệu không hợp lệ");
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Vui lòng nhập họ tên bệnh nhân");
        
        
        var patient = new Patients
        {
            Name = request.Name,
            DateBirth = request.DateBirth,
            Gender = request.Gender,
            Phone = request.Phone?.Trim(),
            Weight = request.Weight
        };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        return Ok(patient);
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
            weight = p.Weight,
            phone = p.Phone,
            age = CalculateAge(p.DateBirth ?? ""),
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