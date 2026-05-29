using CTS_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Role> Roles { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
       modelBuilder.Entity<Role>().ToTable("roles");
    }
}