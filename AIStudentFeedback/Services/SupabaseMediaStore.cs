using System.Net.Http.Headers;

namespace VideoUploadingProject.Services;

public class SupabaseMediaStore : IMediaStore
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _bucket;
    private readonly string _tempRoot;

    public SupabaseMediaStore(HttpClient http, IConfiguration config, IHostEnvironment env)
    {
        _http = http;
        _baseUrl = (config["Storage:SupabaseUrl"] ?? "").TrimEnd('/');
        _bucket = config["Storage:Bucket"] ?? "lectures";
        var key = config["Storage:ServiceRoleKey"] ?? "";
        _tempRoot = Path.Combine(env.ContentRootPath, "App_Data", "tmp");
        Directory.CreateDirectory(_tempRoot);
        _http.Timeout = TimeSpan.FromMinutes(30);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        _http.DefaultRequestHeaders.Add("apikey", key);
    }

    public string ModeName => "Supabase Storage";

    public async Task SaveFromFileAsync(string objectName, string sourcePath, string contentType, CancellationToken ct)
    {
        await using var stream = File.OpenRead(sourcePath);
        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var request = new HttpRequestMessage(HttpMethod.Post, ObjectUrl(objectName)) { Content = content };
        request.Headers.Add("x-upsert", "true");
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Supabase Storage refuzoi ruajtjen ({(int)response.StatusCode}). {Trim(body)}");
        }
    }

    public async Task<(string Path, bool DeleteAfter)> MaterializeAsync(string objectName, CancellationToken ct)
    {
        var destination = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N") + Path.GetExtension(objectName));
        using var response = await _http.GetAsync(ObjectUrl(objectName), ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase Storage nuk e ktheu origjinalin ({(int)response.StatusCode}).");
        await using var file = File.Create(destination);
        await response.Content.CopyToAsync(file, ct);
        return (destination, true);
    }

    public string? TryGetLocalPath(string objectName) => null;

    public string PlaybackUrl(string objectName)
        => $"{_baseUrl}/storage/v1/object/public/{_bucket}/{objectName}";

    private string ObjectUrl(string objectName)
        => $"{_baseUrl}/storage/v1/object/{_bucket}/{objectName}";

    private static string Trim(string text)
        => text.Length <= 240 ? text : text[..240];
}
