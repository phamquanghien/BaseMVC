using BaseMVC.Domain.Faculties;
using BaseMVC.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BaseMVC.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public DbSet<Faculty> Faculties => Set<Faculty>();

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
