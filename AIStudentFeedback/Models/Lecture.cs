namespace VideoUploadingProject.Models;

public class Lecture
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course? Course { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProfessorName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string OriginalObjectName { get; set; } = string.Empty;
    public string Stage { get; set; } = "Received";
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<LectureRendition> Renditions { get; set; } = new();
}
