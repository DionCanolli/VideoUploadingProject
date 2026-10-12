using VideoUploadingProject.Data;
using VideoUploadingProject.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace VideoUploadingProject.Pages.Lectures;

public class IndexModel : PageModel
{
    private readonly UniversityContext _db;

    public IndexModel(UniversityContext db) => _db = db;

    public List<Lecture> Lectures { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            await CourseSeed.EnsureAsync(_db);
            Lectures = await _db.Lectures
                .Include(l => l.Course)
                .OrderByDescending(l => l.CreatedAt)
                .ToListAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Baza e të dhënave nuk u arrit. Kontrollo lidhjen dhe ekzekuto Database/setup.sql në Supabase nëse tabelat Lectures dhe LectureRenditions mungojnë.";
        }
    }
}
