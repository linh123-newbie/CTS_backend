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
    public DbSet<Patients> Patients { get; set; }
    public DbSet<SelfAssessment> SelfAssessment { get; set; }
    public DbSet<QuestionType> QuestionTypes { get; set; }
    public DbSet<BctqQuestion> BctqQuestions { get; set; }
    public DbSet<BctqAnswer> BctqAnswers { get; set; }
    public DbSet<NcsSignalFile> NcsSignalFiles { get; set; }
    public DbSet<NcsResult> NcsResults { get; set; }
    public DbSet<NcsNerveDetail> NcsNerveDetails { get; set; }
    public DbSet<UltrasoundResult> UltrasoundResults { get; set; }
    public DbSet<ClinicalRecord> ClinicalRecords { get; set; }
    public DbSet<PhysicalTest> PhysicalTests { get; set; }
    public DbSet<Exercises> Exercises { get; set; }
    public DbSet<ClinicalRecord> ClinicalRecord { get; set; }
    public DbSet<Staffs> Staffs { get; set; }
    public DbSet<AssessmentAnswer> AssessmentAnswer { get; set; }
    public DbSet<AssessmentPhysicalDetail> AssessmentPhysicalDetail { get; set; }
    public DbSet<AssessmentSymptomArea> AssessmentSymptomArea { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Roles>().ToTable("roles");
        modelBuilder.Entity<ClinicalRecord>().ToTable("clinical_record");
        modelBuilder.Entity<Staffs>().ToTable("staffs");
        modelBuilder.Entity<Patients>().ToTable("patients");
        modelBuilder.Entity<SelfAssessment>().ToTable("self_assessment");
        modelBuilder.Entity<AssessmentAnswer>().ToTable("assessment_answer");
        modelBuilder.Entity<AssessmentPhysicalDetail>().ToTable("assessment_physical_detail");
        modelBuilder.Entity<AssessmentSymptomArea>().ToTable("assessment_symptom_area");
        modelBuilder.Entity<Users>().ToTable("users");
        modelBuilder.Entity<NcsSignalFile>().ToTable("ncs_signal_file");
        modelBuilder.Entity<UltrasoundResult>().ToTable("ultrasound_result");
        modelBuilder.Entity<NcsNerveDetail>().ToTable("ncs_nerve_detail");
        modelBuilder.Entity<NcsResult>().ToTable("ncs_result");
        modelBuilder.Entity<ClinicalRecord>().ToTable("clinical_record");
        modelBuilder.Entity<QuestionType>().ToTable("question_type");
        modelBuilder.Entity<BctqQuestion>().ToTable("bctq_question");
        modelBuilder.Entity<BctqAnswer>().ToTable("bctq_answer");
        modelBuilder.Entity<Exercises>().ToTable("exercise");
        modelBuilder.Entity<PhysicalTest>().ToTable("physical_test");
        modelBuilder.Entity<BctqAnswer>()
         .HasOne(a => a.BctqQuestion)
         .WithMany(q => q.Answers)
         .HasForeignKey(a => a.BctqQuestionId);

        modelBuilder.Entity<SelfAssessment>()
    .HasOne(sa => sa.Users)
    .WithMany(u => u.SelfAssessments)
    .HasForeignKey(sa => sa.UserId);
    }
}