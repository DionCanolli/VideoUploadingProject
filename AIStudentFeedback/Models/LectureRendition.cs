namespace VideoUploadingProject.Models;

public class LectureRendition
{
    public int Id { get; set; }
    public int LectureId { get; set; }
    public Lecture? Lecture { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public int Height { get; set; }
    public string ObjectName { get; set; } = string.Empty;
    public string PlaybackUrl { get; set; } = string.Empty;
}
