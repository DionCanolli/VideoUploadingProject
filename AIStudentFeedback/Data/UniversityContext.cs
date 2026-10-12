using VideoUploadingProject.Models;
using Microsoft.EntityFrameworkCore;

namespace VideoUploadingProject.Data;

public class UniversityContext : DbContext
{
    public UniversityContext(DbContextOptions<UniversityContext> options) : base(options) { }

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<LectureRendition> LectureRenditions => Set<LectureRendition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lecture>()
            .HasOne(l => l.Course)
            .WithMany(c => c.Lectures)
            .HasForeignKey(l => l.CourseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LectureRendition>()
            .HasOne(r => r.Lecture)
            .WithMany(l => l.Renditions)
            .HasForeignKey(r => r.LectureId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
