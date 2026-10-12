using VideoUploadingProject.Data;
using VideoUploadingProject.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000;
});

builder.Services.AddRazorPages();
builder.Services.AddDbContext<UniversityContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var storageMode = builder.Configuration["Storage:Mode"];
var serviceKey = builder.Configuration["Storage:ServiceRoleKey"];
if (string.Equals(storageMode, "Supabase", StringComparison.OrdinalIgnoreCase)
    && !string.IsNullOrWhiteSpace(serviceKey))
{
    builder.Services.AddHttpClient<IMediaStore, SupabaseMediaStore>();
}
else
{
    builder.Services.AddSingleton<IMediaStore, LocalMediaStore>();
}

builder.Services.AddSingleton<LectureWorkQueue>();
builder.Services.AddScoped<LecturePipeline>();
builder.Services.AddHostedService<LecturePipelineWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();

app.MapGet("/media/{lectureId:int}/{file}", async (
    int lectureId,
    string file,
    UniversityContext db,
    IMediaStore store,
    CancellationToken ct) =>
{
    if (file.Contains("..") || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        return Results.BadRequest();

    var objectName = $"{lectureId}/{file}";
    var known = await db.LectureRenditions.AnyAsync(r => r.LectureId == lectureId && r.ObjectName == objectName, ct);
    if (!known)
    {
        known = await db.Lectures.AnyAsync(l => l.Id == lectureId && l.OriginalObjectName == objectName, ct);
    }
    if (!known)
        return Results.NotFound();

    var path = store.TryGetLocalPath(objectName);
    if (path == null)
        return Results.NotFound();

    var contentType = file.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4";
    return Results.File(path, contentType, enableRangeProcessing: true);
});

app.Run();
