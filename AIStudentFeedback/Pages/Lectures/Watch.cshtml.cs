using VideoUploadingProject.Data;
using VideoUploadingProject.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace VideoUploadingProject.Pages.Lectures;

public class WatchModel : PageModel
{
    private readonly UniversityContext _db;

    public WatchModel(UniversityContext db) => _db = db;

    public Lecture? Lecture { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Lecture = await _db.Lectures
                .Include(l => l.Course)
                .Include(l => l.Renditions)
                .FirstOrDefaultAsync(l => l.Id == id);
            if (Lecture == null)
                return NotFound();
            return Page();
        }
        catch (Exception)
        {
            ErrorMessage = "Baza e të dhënave nuk u arrit.";
            return Page();
        }
    }

    public async Task<IActionResult> OnGetStatusAsync(int id)
    {
        var lecture = await _db.Lectures.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
        if (lecture == null)
            return NotFound();
        return new JsonResult(new
        {
            stage = lecture.Stage,
            detail = lecture.Detail,
            ready = lecture.Stage == "Ready",
            failed = lecture.Stage == "Failed"
        });
    }
}
