using VideoUploadingProject.Data;
using VideoUploadingProject.Models;
using VideoUploadingProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace VideoUploadingProject.Pages.Lectures;

[RequestSizeLimit(524_288_000)]
[RequestFormLimits(MultipartBodyLengthLimit = 524_288_000)]
public class UploadModel : PageModel
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".webm", ".avi", ".m4v"
    };

    private readonly UniversityContext _db;
    private readonly IMediaStore _store;
    private readonly LectureWorkQueue _queue;

    public UploadModel(UniversityContext db, IMediaStore store, LectureWorkQueue queue)
    {
        _db = db;
        _store = store;
        _queue = queue;
    }

    public List<SelectListItem> Courses { get; set; } = new();
    public string? ErrorMessage { get; set; }

    [BindProperty] public int CourseId { get; set; }
    [BindProperty] public string Title { get; set; } = string.Empty;
    [BindProperty] public string ProfessorName { get; set; } = string.Empty;
    [BindProperty] public IFormFile? Video { get; set; }

    public async Task OnGetAsync() => await LoadCoursesAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCoursesAsync();
        if (ErrorMessage != null)
            return Page();

        Title = Title.Trim();
        ProfessorName = ProfessorName.Trim();
        if (Title.Length is < 2 or > 160)
            ModelState.AddModelError(nameof(Title), "Titulli duhet të ketë 2 deri në 160 shkronja.");
        if (ProfessorName.Length is < 2 or > 120)
            ModelState.AddModelError(nameof(ProfessorName), "Shkruaj emrin e profesorit.");
        if (Video == null || Video.Length == 0)
            ModelState.AddModelError(nameof(Video), "Zgjidh një video.");
        else if (Video.Length > 524_288_000)
            ModelState.AddModelError(nameof(Video), "Videoja duhet të jetë më e vogël se 500 MB.");
        else if (!Allowed.Contains(Path.GetExtension(Video.FileName)))
            ModelState.AddModelError(nameof(Video), "Lejohen MP4, MOV, MKV, WebM, AVI dhe M4V.");

        var courseOk = await _db.Courses.AnyAsync(c => c.Id == CourseId);
        if (!courseOk)
            ModelState.AddModelError(nameof(CourseId), "Zgjidh lëndën.");

        if (!ModelState.IsValid || Video == null)
            return Page();

        Lecture lecture;
        try
        {
            lecture = new Lecture
            {
                CourseId = CourseId,
                Title = Title,
                ProfessorName = ProfessorName,
                OriginalFileName = Path.GetFileName(Video.FileName),
                OriginalObjectName = "pending",
                Stage = "Received",
                Detail = "Videoja u pranua.",
                CreatedAt = DateTime.UtcNow
            };
            _db.Lectures.Add(lecture);
            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Baza e të dhënave nuk u arrit. Ekzekuto Database/setup.sql në Supabase, pastaj rifresko faqen.";
            return Page();
        }

        var extension = Path.GetExtension(Video.FileName).ToLowerInvariant();
        var temp = Path.Combine(Path.GetTempPath(), "upload-" + lecture.Id + extension);
        try
        {
            await using (var stream = System.IO.File.Create(temp))
                await Video.CopyToAsync(stream);

            lecture.OriginalObjectName = $"{lecture.Id}/original{extension}";
            await _store.SaveFromFileAsync(lecture.OriginalObjectName, temp, Video.ContentType ?? "application/octet-stream", HttpContext.RequestAborted);
            lecture.Stage = "Stored";
            lecture.Detail = "Videoja u ruajt te " + _store.ModeName + ".";
            await _db.SaveChangesAsync();
            _queue.Enqueue(lecture.Id);
            return Redirect($"/Shiko/{lecture.Id}");
        }
        catch (Exception)
        {
            lecture.Stage = "Failed";
            lecture.Detail = "Videoja u pranua, por nuk u ruajt në ruajtje.";
            await _db.SaveChangesAsync();
            ErrorMessage = lecture.Detail;
            return Page();
        }
        finally
        {
            if (System.IO.File.Exists(temp))
                System.IO.File.Delete(temp);
        }
    }

    private async Task LoadCoursesAsync()
    {
        try
        {
            await CourseSeed.EnsureAsync(_db);
            Courses = await _db.Courses
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem(c.Code + " · " + c.Name, c.Id.ToString()))
                .ToListAsync();
        }
        catch (Exception)
        {
            ErrorMessage = "Baza e të dhënave nuk u arrit. Ekzekuto Database/setup.sql në Supabase, pastaj rifresko faqen.";
        }
    }
}
