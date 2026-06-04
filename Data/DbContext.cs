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
    public DbSet<QuestionType> QuestionTypes { get; set; }
    public DbSet<BctqQuestion> BctqQuestions { get; set; }
    public DbSet<BctqAnswer> BctqAnswers { get; set; }
    public DbSet<PhysicalTest> PhysicalTests { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Roles>().ToTable("roles");
        modelBuilder.Entity<Users>().ToTable("users");
        modelBuilder.Entity<QuestionType>().ToTable("question_type");
        modelBuilder.Entity<BctqQuestion>().ToTable("bctq_question");
        modelBuilder.Entity<BctqAnswer>().ToTable("bctq_answer");
        modelBuilder.Entity<PhysicalTest>().ToTable("physical_test");
        modelBuilder.Entity<BctqAnswer>()
         .HasOne(a => a.BctqQuestion)
         .WithMany(q => q.Answers)
         .HasForeignKey(a => a.BctqQuestionId);
    }
}