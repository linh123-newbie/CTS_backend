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
        return await Task.FromResult(Ok("hello linh beautiful hihih"));
    }
    [HttpGet("test1")]
    public async Task<ActionResult<string>> Test1()
    {
        return await Task.FromResult(Ok("i will have a good job after graduation"));
    }
    [HttpGet("test2")]
    public async Task<ActionResult<string>> Test2()
    {
        return await Task.FromResult(Ok("i will have extra data for my thesis"));
    }
}