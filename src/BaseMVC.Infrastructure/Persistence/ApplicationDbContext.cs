using BaseMVC.Domain.ExamTypes;
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

        builder.Entity<ExamType>(entity =>
        {
            entity.HasKey(examType => examType.Id);
            entity.Property(examType => examType.Name).HasMaxLength(ExamType.NameMaxLength).IsRequired();
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
}
