using VideoUploadingProject.Models;
using Microsoft.EntityFrameworkCore;

namespace VideoUploadingProject.Data;

public static class CourseSeed
{
    public static async Task EnsureAsync(UniversityContext db)
    {
        if (await db.Courses.AnyAsync())
            return;

        db.Courses.Add(new Course
        {
            Code = "CC101",
            Name = "Cloud Computing",
            Description = "Ligjërata online për ruajtjen, përpunimin dhe shpërndarjen e videove në cloud."
        });
        await db.SaveChangesAsync();
    }
}
