using Microsoft.EntityFrameworkCore;

namespace StudyFlow.Web.Data;

public sealed class StudyFlowDbContext(DbContextOptions<StudyFlowDbContext> options) : DbContext(options)
{
    internal DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();
    internal DbSet<StudyTaskEntity> Tasks => Set<StudyTaskEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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
