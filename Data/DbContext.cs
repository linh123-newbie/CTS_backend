using CTS_backend.Models;
using Microsoft.EntityFrameworkCore;

namespace CTS_backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Roles> Roles { get; set; }
    public DbSet<Users> Users { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
       modelBuilder.Entity<Roles>().ToTable("roles");
       modelBuilder.Entity<Users>().ToTable("users");
    }
}