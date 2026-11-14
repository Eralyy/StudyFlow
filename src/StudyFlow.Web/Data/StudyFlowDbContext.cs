using Microsoft.EntityFrameworkCore;

namespace StudyFlow.Web.Data;

public sealed class StudyFlowDbContext(DbContextOptions<StudyFlowDbContext> options) : DbContext(options)
{
    internal DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();
    internal DbSet<StudyTaskEntity> Tasks => Set<StudyTaskEntity>();
    internal DbSet<AvailabilityEntity> Availability => Set<AvailabilityEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var availability = modelBuilder.Entity<AvailabilityEntity>();
        availability.ToTable("Availability", table =>
        {
            table.HasCheckConstraint("CK_Availability_Day", "Day BETWEEN 0 AND 6");
            table.HasCheckConstraint("CK_Availability_Minutes", "Minutes BETWEEN 0 AND 720");
        });
        availability.HasKey(item => item.Day);
        availability.Property(item => item.Day).HasConversion<int>().ValueGeneratedNever();

        var subjects = modelBuilder.Entity<SubjectEntity>();
        subjects.ToTable("Subjects");
        subjects.HasKey(subject => subject.Id);
        subjects.Property(subject => subject.Name).HasMaxLength(60).IsRequired();
        subjects.Property(subject => subject.NameKey).HasMaxLength(60).IsRequired();
        subjects.Property(subject => subject.Colour).HasMaxLength(7).IsRequired();
        subjects.HasIndex(subject => subject.NameKey).IsUnique();

        var tasks = modelBuilder.Entity<StudyTaskEntity>();
        tasks.ToTable("Tasks");
        tasks.HasKey(task => task.Id);
        tasks.Property(task => task.Title).HasMaxLength(120).IsRequired();
        tasks.Property(task => task.Notes).HasMaxLength(1000).IsRequired();
        tasks.Property(task => task.Priority).HasConversion<int>();
        tasks.HasIndex(task => task.SubjectId);
        tasks.HasIndex(task => new { task.IsCompleted, task.DueDate });
        tasks.HasOne(task => task.Subject).WithMany(subject => subject.Tasks)
            .HasForeignKey(task => task.SubjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
