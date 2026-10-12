namespace VideoUploadingProject.Services;

public class LocalMediaStore : IMediaStore
{
    private readonly string _root;

    public LocalMediaStore(IHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "App_Data", "media");
        Directory.CreateDirectory(_root);
    }

    public string ModeName => "ruajtjen lokale (App_Data)";

    public Task SaveFromFileAsync(string objectName, string sourcePath, string contentType, CancellationToken ct)
    {
        var destination = Physical(objectName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(sourcePath, destination, overwrite: true);
        return Task.CompletedTask;
    }

    public Task<(string Path, bool DeleteAfter)> MaterializeAsync(string objectName, CancellationToken ct)
    {
        var path = Physical(objectName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Skedari origjinal nuk u gjet në ruajtje.", path);
        return Task.FromResult((path, false));
    }

    public string? TryGetLocalPath(string objectName)
    {
        var path = Physical(objectName);
        return File.Exists(path) ? path : null;
    }

    public string PlaybackUrl(string objectName)
    {
        var file = Path.GetFileName(objectName);
        var folder = Path.GetFileName(Path.GetDirectoryName(objectName));
        return $"/media/{folder}/{file}";
    }

    private string Physical(string objectName)
    {
        var parts = objectName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(p => p is "." or ".." || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            throw new InvalidOperationException("Emri i skedarit nuk lejohet.");
        return Path.Combine(new[] { _root }.Concat(parts).ToArray());
    }
}
