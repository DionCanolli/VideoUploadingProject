namespace VideoUploadingProject.Services;

public interface IMediaStore
{
    string ModeName { get; }

    Task SaveFromFileAsync(string objectName, string sourcePath, string contentType, CancellationToken ct);

    /// <summary>A local path ffmpeg can read. Delete the file afterwards only when <paramref name="deleteAfter"/> is true.</summary>
    Task<(string Path, bool DeleteAfter)> MaterializeAsync(string objectName, CancellationToken ct);

    string? TryGetLocalPath(string objectName);

    string PlaybackUrl(string objectName);
}
