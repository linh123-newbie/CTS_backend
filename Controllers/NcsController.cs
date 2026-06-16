using CTS_backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NcsController : ControllerBase
{
    private readonly AppDbContext _context;

    public NcsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("getNcsResults")]
    public async Task<ActionResult> GetNcsResults(
        [FromQuery] int doctorUserId)
    {
        var query =
            from n in _context.NcsResults
            join c in _context.ClinicalRecords
                on n.ClinicalRecordId equals c.Id
            join p in _context.Patients
                on c.PatientId equals p.Id
            join s in _context.Staffs
                on c.DoctorId equals s.Id
            where s.UserId == doctorUserId
            select new
            {
                id = n.Id,
                clinicalRecordId = c.Id,
                patientId = p.Id,
                patientName = p.Name,
                hand = n.Hand,
                handText = n.Hand == 1 ? "Tay phải" : "Tay trái",
                label = n.Label,
                status = n.Status,
                time = c.Time
            };


        var data = await query
            .OrderByDescending(x => x.time)
            .ToListAsync();

        return Ok(data);
    }
}