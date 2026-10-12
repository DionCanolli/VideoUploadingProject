using System.Diagnostics;
using System.Text;
using VideoUploadingProject.Data;
using VideoUploadingProject.Models;
using Microsoft.EntityFrameworkCore;

namespace VideoUploadingProject.Services;

public class LecturePipeline
{
    private readonly UniversityContext _db;
    private readonly IMediaStore _store;
    private readonly ILogger<LecturePipeline> _logger;

    public LecturePipeline(UniversityContext db, IMediaStore store, ILogger<LecturePipeline> logger)
    {
        _db = db;
        _store = store;
        _logger = logger;
    }

    public async Task RunAsync(int lectureId, CancellationToken ct)
    {
        var lecture = await _db.Lectures
            .Include(l => l.Renditions)
            .FirstOrDefaultAsync(l => l.Id == lectureId, ct);
        if (lecture == null || lecture.Stage is "Ready" or "Failed")
            return;

        string? sourcePath = null;
        var deleteSource = false;
        var work = Path.Combine(Path.GetTempPath(), "lecture-" + lecture.Id);
        try
        {
            var ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
            {
                await SetAsync(lecture, "Failed", "FFmpeg nuk u gjet. Instaloje, pastaj rinise programin dhe ngarko videon përsëri.", ct);
                return;
            }

            Directory.CreateDirectory(work);
            (sourcePath, deleteSource) = await _store.MaterializeAsync(lecture.OriginalObjectName, ct);

            await SetAsync(lecture, "Converting", "Po konvertohet në MP4 dhe WebM.", ct);
            try
            {
                await EncodeAsync(ffmpeg, lecture, sourcePath, work, new RenditionSpec("WebM 720p", "webm", 720, "720p.webm", "video/webm"), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebM conversion failed for lecture {LectureId}", lecture.Id);
            }

            await SetAsync(lecture, "Converting", "Po krijohen versionet 480p, 720p dhe 1080p.", ct);
            await EncodeAsync(ffmpeg, lecture, sourcePath, work, new RenditionSpec("480p", "mp4", 480, "480p.mp4", "video/mp4"), ct);
            await EncodeAsync(ffmpeg, lecture, sourcePath, work, new RenditionSpec("720p", "mp4", 720, "720p.mp4", "video/mp4"), ct);
            await EncodeAsync(ffmpeg, lecture, sourcePath, work, new RenditionSpec("1080p", "mp4", 1080, "1080p.mp4", "video/mp4"), ct);

            await SetAsync(lecture, "RenditionsReady", "U krijuan versionet 480p, 720p dhe 1080p, plus WebM.", ct);
            await SetAsync(lecture, "Distributed", "Versionet u vendosën te " + _store.ModeName + ".", ct);
            await SetAsync(lecture, "Ready", "Studentët mund ta shikojnë videon përmes internetit.", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lecture {LectureId} failed during processing", lectureId);
            var message = ex.Message.Length <= 400 ? ex.Message : ex.Message[..400];
            await SetAsync(lecture, "Failed", message, ct);
        }
        finally
        {
            if (deleteSource && sourcePath != null)
                TryDelete(sourcePath);
            TryDeleteDirectory(work);
        }
    }

    private async Task EncodeAsync(string ffmpeg, Lecture lecture, string sourcePath, string work, RenditionSpec spec, CancellationToken ct)
    {
        var output = Path.Combine(work, spec.FileName);
        var args = new List<string> { "-y", "-i", sourcePath, "-map", "0:v:0", "-map", "0:a:0?", "-vf", $"scale=-2:{spec.Height}" };
        if (spec.Format == "webm")
        {
            args.AddRange(new[] { "-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-b:v", "1M", "-c:a", "libopus", "-b:a", "96k" });
        }
        else
        {
            args.AddRange(new[] { "-c:v", "libx264", "-preset", "veryfast", "-crf", "26", "-c:a", "aac", "-b:a", "128k", "-movflags", "+faststart" });
        }
        args.Add(output);

        var error = await RunProcessAsync(ffmpeg, args, ct);
        if (error != null)
            throw new InvalidOperationException($"Konvertimi {spec.Label} dështoi. {error}");

        var objectName = $"{lecture.Id}/{spec.FileName}";
        await _store.SaveFromFileAsync(objectName, output, spec.ContentType, ct);

        var existing = lecture.Renditions.FirstOrDefault(r => r.ObjectName == objectName);
        if (existing == null)
        {
            existing = new LectureRendition { ObjectName = objectName };
            lecture.Renditions.Add(existing);
        }
        existing.Label = spec.Label;
        existing.Format = spec.Format;
        existing.Height = spec.Height;
        existing.PlaybackUrl = _store.PlaybackUrl(objectName);
        await _db.SaveChangesAsync(ct);
    }

    private async Task SetAsync(Lecture lecture, string stage, string detail, CancellationToken ct)
    {
        lecture.Stage = stage;
        lecture.Detail = detail;
        await _db.SaveChangesAsync(ct);
    }

    private static async Task<string?> RunProcessAsync(string fileName, List<string> args, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.RedirectStandardOutput = true;
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            if (stderr.Length < 2500) stderr.AppendLine(e.Data);
        };
        process.OutputDataReceived += (_, _) => { };

        process.Start();
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(45));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new InvalidOperationException("Konvertimi zgjati shumë dhe u ndërpre.");
        }

        if (process.ExitCode == 0)
            return null;

        var text = stderr.ToString().Trim();
        return text.Length <= 300 ? text : text[^300..];
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }

    public static string? FindFfmpeg()
    {
        var names = new[] { "ffmpeg.exe", "ffmpeg" };
        var path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var name in names)
                {
                    var candidate = Path.Combine(folder.Trim(), name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
        }

        string[] known =
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WinGet\Links\ffmpeg.exe")
        };
        return known.FirstOrDefault(File.Exists);
    }

    private sealed record RenditionSpec(string Label, string Format, int Height, string FileName, string ContentType);
}

public class LecturePipelineWorker : BackgroundService
{
    private readonly LectureWorkQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<LecturePipelineWorker> _logger;

    public LecturePipelineWorker(LectureWorkQueue queue, IServiceScopeFactory scopes, ILogger<LecturePipelineWorker> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var lectureId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<LecturePipeline>();
                await pipeline.RunAsync(lectureId, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Lecture worker failed for {LectureId}", lectureId);
            }
        }
    }
}
