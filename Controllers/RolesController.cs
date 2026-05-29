using CTS_backend.Data;
using CTS_backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly AppDbContext _context;

    public RolesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<Role>>> GetRoles()
    {
        return await _context.Roles.ToListAsync();
    }
    [HttpGet("test")]
    public async Task<ActionResult<string>> Test()
    {
        return await Task.FromResult(Ok("hello"));
    }
}