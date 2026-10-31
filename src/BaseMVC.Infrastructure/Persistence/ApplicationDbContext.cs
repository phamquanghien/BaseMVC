using BaseMVC.Domain.ExamTypes;
using BaseMVC.Domain.Exams;
using BaseMVC.Domain.Faculties;
using BaseMVC.Domain.Students;
using BaseMVC.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<Faculty> Faculties => Set<Faculty>();

    public DbSet<ExamType> ExamTypes => Set<ExamType>();

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<ItExamCandidate> ItExamCandidates => Set<ItExamCandidate>();

    public DbSet<ItExamResult> ItExamResults => Set<ItExamResult>();

    public DbSet<LanguageStandardCandidate> LanguageStandardCandidates => Set<LanguageStandardCandidate>();

    public DbSet<LanguageStandardResult> LanguageStandardResults => Set<LanguageStandardResult>();

    public DbSet<LanguagePlacementCandidate> LanguagePlacementCandidates => Set<LanguagePlacementCandidate>();

    public DbSet<LanguagePlacementResult> LanguagePlacementResults => Set<LanguagePlacementResult>();

    public DbSet<EnglishIntensiveResult> EnglishIntensiveResults => Set<EnglishIntensiveResult>();

    public DbSet<ItBasicCertificateCandidate> ItBasicCertificateCandidates => Set<ItBasicCertificateCandidate>();

    public DbSet<ItBasicCertificateResult> ItBasicCertificateResults => Set<ItBasicCertificateResult>();

    public DbSet<Student> Students => Set<Student>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Faculty>(entity =>
        {
            entity.HasKey(faculty => faculty.Id);
            entity.Property(faculty => faculty.FacultyCode)
                .HasMaxLength(Faculty.FacultyCodeLength)
                .IsRequired();
            entity.Property(faculty => faculty.FacultyName)
                .HasMaxLength(Faculty.FacultyNameMaxLength)
                .IsRequired();
            entity.Property(faculty => faculty.CreatedBy).HasMaxLength(450);
            entity.Property(faculty => faculty.LastModifiedBy).HasMaxLength(450);
            entity.HasIndex(faculty => faculty.FacultyCode).IsUnique();
        });

        builder.Entity<Exam>(entity =>
        {
            entity.HasKey(exam => exam.Id);
            entity.Property(exam => exam.Name).HasMaxLength(Exam.NameMaxLength).IsRequired();
            entity.Property(exam => exam.Status).HasConversion<string>().HasMaxLength(Exam.StatusMaxLength).IsRequired();
            entity.Property(exam => exam.CreatedBy).HasMaxLength(450);
            entity.Property(exam => exam.LastModifiedBy).HasMaxLength(450);
            entity.HasOne(exam => exam.ExamType).WithMany().HasForeignKey(exam => exam.ExamTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(exam => !exam.IsDeleted);
        });

        builder.Entity<ItExamCandidate>(entity =>
        {
            entity.HasKey(candidate => candidate.Id);
            entity.Property(candidate => candidate.Session).HasMaxLength(ItExamCandidate.ShortTextMaxLength);
            entity.Property(candidate => candidate.ExamDuration).HasMaxLength(ItExamCandidate.ShortTextMaxLength);
            entity.Property(candidate => candidate.ExamRoom).HasMaxLength(ItExamCandidate.ShortTextMaxLength);
            entity.Property(candidate => candidate.RetentionNote).HasMaxLength(ItExamCandidate.NoteMaxLength);
            entity.Property(candidate => candidate.RetainedSection).HasMaxLength(ItExamCandidate.NoteMaxLength);
            entity.Property(candidate => candidate.CreatedBy).HasMaxLength(450);
            entity.Property(candidate => candidate.LastModifiedBy).HasMaxLength(450);
            entity.HasOne(candidate => candidate.Exam).WithMany().HasForeignKey(candidate => candidate.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(candidate => candidate.Student).WithMany().HasForeignKey(candidate => candidate.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(candidate => new { candidate.ExamId, candidate.StudentId }).IsUnique();
            entity.HasQueryFilter(candidate => !candidate.Exam!.IsDeleted);
        });

        builder.Entity<ItExamResult>(entity =>
        {
            entity.HasKey(result => result.Id);
            entity.Property(result => result.MultipleChoiceScore).HasMaxLength(ItExamResult.ScoreMaxLength);
            entity.Property(result => result.PracticalScore).HasMaxLength(ItExamResult.ScoreMaxLength);
            entity.Property(result => result.FinalScore).HasMaxLength(ItExamResult.ScoreMaxLength);
            entity.Property(result => result.Note).HasMaxLength(ItExamResult.NoteMaxLength);
            entity.Property(result => result.CreatedBy).HasMaxLength(450);
            entity.Property(result => result.LastModifiedBy).HasMaxLength(450);
            entity.HasOne(result => result.Exam).WithMany().HasForeignKey(result => result.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(result => result.Student).WithMany().HasForeignKey(result => result.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(result => new { result.ExamId, result.StudentId }).IsUnique();
            entity.HasQueryFilter(result => !result.Exam!.IsDeleted);
        });

        builder.Entity<ExamType>(entity =>
        {
            entity.HasKey(examType => examType.Id);
            entity.Property(examType => examType.Name).HasMaxLength(ExamType.NameMaxLength).IsRequired();
            entity.Property(examType => examType.DataFormat).HasConversion<string>().HasMaxLength(32);
            entity.Property(examType => examType.CreatedBy).HasMaxLength(450);
            entity.Property(examType => examType.LastModifiedBy).HasMaxLength(450);
            entity.HasIndex(examType => examType.Name).IsUnique();
        });

        builder.Entity<Student>(entity =>
        {
            entity.HasKey(student => student.Id);
            entity.Property(student => student.StudentCode).HasMaxLength(Student.StudentCodeMaxLength).IsRequired();
            entity.Property(student => student.LastName).HasMaxLength(Student.NameMaxLength).IsRequired();
            entity.Property(student => student.FirstName).HasMaxLength(Student.NameMaxLength).IsRequired();
            entity.Property(student => student.ClassCode).HasMaxLength(Student.ClassCodeMaxLength).IsRequired();
            entity.Property(student => student.FacultyCode).HasMaxLength(Student.FacultyCodeLength).IsRequired();
            entity.Property(student => student.Gender).HasDefaultValue(false);
            entity.Property(student => student.EligibleForEnglish).HasDefaultValue(false);
            entity.Property(student => student.PassedLanguageRequirement).HasDefaultValue(false);
            entity.Property(student => student.PassedITRequirement).HasDefaultValue(false);
            entity.Property(student => student.CreatedBy).HasMaxLength(450);
            entity.Property(student => student.LastModifiedBy).HasMaxLength(450);
            entity.HasIndex(student => student.StudentCode).IsUnique();
            entity.HasIndex(student => student.FacultyCode);
            entity.HasOne<Faculty>()
                .WithMany()
                .HasPrincipalKey(faculty => faculty.FacultyCode)
                .HasForeignKey(student => student.FacultyCode)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureStudentRecord<LanguageStandardCandidate>(builder);
        ConfigureStudentRecord<LanguageStandardResult>(builder);
        ConfigureStudentRecord<LanguagePlacementCandidate>(builder);
        ConfigureStudentRecord<LanguagePlacementResult>(builder);
        ConfigureStudentRecord<EnglishIntensiveResult>(builder);
        ConfigureIndependentRecord<ItBasicCertificateCandidate>(builder);
        ConfigureIndependentRecord<ItBasicCertificateResult>(builder);
        ApplyExamDataLimits(builder);

        builder.Entity<AuditLogEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.EventType)
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(entry => entry.Outcome)
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(entry => entry.ActorUserId).HasMaxLength(450);
            entity.Property(entry => entry.TargetUserId).HasMaxLength(450);
            entity.Property(entry => entry.TargetRoleId).HasMaxLength(450);
            entity.HasIndex(entry => entry.OccurredAtUtc);
            entity.HasIndex(entry => entry.ActorUserId);
            entity.HasIndex(entry => entry.TargetUserId);
            entity.HasIndex(entry => entry.TargetRoleId);
        });
    }

    private static void ConfigureExamRecord<T>(ModelBuilder builder) where T : ExamRecord =>
        builder.Entity<T>(entity =>
        {
            entity.HasKey(record => record.Id);
            entity.Property(record => record.CreatedBy).HasMaxLength(450);
            entity.Property(record => record.LastModifiedBy).HasMaxLength(450);
            entity.HasOne(record => record.Exam).WithMany().HasForeignKey(record => record.ExamId).OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(record => !record.Exam!.IsDeleted);
        });

    private static void ConfigureStudentRecord<T>(ModelBuilder builder) where T : StudentExamRecord
    {
        ConfigureExamRecord<T>(builder);
        builder.Entity<T>(entity =>
        {
            entity.HasOne(record => record.Student).WithMany().HasForeignKey(record => record.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(record => new { record.ExamId, record.StudentId }).IsUnique();
        });
    }

    private static void ConfigureIndependentRecord<T>(ModelBuilder builder) where T : ExamRecord
    {
        ConfigureExamRecord<T>(builder);
        builder.Entity<T>(entity =>
        {
            entity.HasIndex(nameof(ExamRecord.ExamId), "IdentityNumber").IsUnique();
            entity.HasIndex(nameof(ExamRecord.ExamId), "CandidateNumber").IsUnique();
        });
    }

    private static void ApplyExamDataLimits(ModelBuilder builder)
    {
        var properties = builder.Model.GetEntityTypes()
            .Where(type => typeof(ExamRecord).IsAssignableFrom(type.ClrType))
            .SelectMany(type => type.GetProperties())
            .Where(property => property.ClrType == typeof(string) && property.GetMaxLength() is null);
        foreach (var property in properties.ToList())
            property.SetMaxLength(ExamDataLimits.For(property.Name));
    }
}
