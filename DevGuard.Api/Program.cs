using DevGuard.Api.Data;
using DevGuard.Api.Models;
using DevGuard.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<AppSettingsService>();
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddScoped<ProjectScannerService>();
builder.Services.AddScoped<GitHubService>();
builder.Services.AddScoped<SecretScannerService>();
builder.Services.AddHttpClient<DependencyScannerService>();
builder.Services.AddScoped<FileFilterService>();
builder.Services.AddScoped<GeminiService>();
builder.Services.AddScoped<ScanPipelineService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Tự động Migrate và Seed Data Settings ban đầu
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var settingsService = scope.ServiceProvider.GetRequiredService<AppSettingsService>();
    await settingsService.SeedDefaultSettingsAsync();
}

app.UseCors("AllowAll");
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger"));

// === API QUẢN LÝ APP SETTINGS ===

// 1. Lấy danh sách tất cả các cấu hình
app.MapGet("/api/settings", async (AppDbContext db) => 
    await db.AppSettings.ToListAsync());

// 2. Cập nhật hoặc Thêm mới một Key cấu hình
app.MapPost("/api/settings", async (AppSetting setting, AppSettingsService settingsService) =>
{
    await settingsService.SetSettingAsync(setting.Key, setting.Value, setting.Category, setting.Description);
    return Results.Ok(setting);
});

// === API PROJECT & SCANNER ===

app.MapGet("/api/projects", async (AppDbContext db) => 
    await db.Projects.Include(p => p.Issues).ToListAsync());

app.MapPost("/api/projects", async (RegisteredProject project, AppDbContext db, ProjectScannerService scanner) =>
{
    db.Projects.Add(project);
    await db.SaveChangesAsync();

    string path = scanner.FetchProjectPath(project);
    var issues = await scanner.ScanDirectoryAsync(project.Id, path);
    
    db.Issues.AddRange(issues);
    await db.SaveChangesAsync();

    return Results.Created($"/api/projects/{project.Id}", project);
});

app.MapGet("/api/projects/{id:guid}/issues", async (Guid id, AppDbContext db) =>
{
    var issues = await db.Issues
        .Where(i => i.RegisteredProjectId == id)
        .ToListAsync();

    return Results.Ok(issues);
});

app.MapPost("/api/issues/{id}/status", async (Guid id, string status, AppDbContext db) =>
{
    var issue = await db.Issues.FindAsync(id);
    if (issue == null) return Results.NotFound();

    issue.Status = status; // "Approved" hoặc "Rejected"
    await db.SaveChangesAsync();
    return Results.Ok(issue);
});

app.MapGet("/api/projects/{projectId:guid}/chat", async (Guid projectId, AppDbContext db) =>
{
    var messages = await db.ChatMessages
        .Where(m => m.RegisteredProjectId == projectId)
        .OrderBy(m => m.CreatedAt)
        .ToListAsync();
    return Results.Ok(messages);
});

// 2. Gửi câu hỏi cho Gemini AI và lưu vào lịch sử
app.MapPost("/api/projects/{projectId:guid}/chat", async (Guid projectId, ChatMessage userMsg, AppDbContext db, GeminiService gemini) =>
{
    userMsg.RegisteredProjectId = projectId;
    userMsg.Role = "user";
    userMsg.CreatedAt = DateTime.UtcNow;
    db.ChatMessages.Add(userMsg);
    await db.SaveChangesAsync();

    // Lấy lịch sử 10 câu gần nhất để giữ context
    var history = await db.ChatMessages
        .Where(m => m.RegisteredProjectId == projectId)
        .OrderByDescending(m => m.CreatedAt)
        .Take(10)
        .OrderBy(m => m.CreatedAt)
        .ToListAsync();

    // Gọi Gemini trả lời dựa trên context
    string aiReply = await gemini.ChatWithProjectContextAsync(history, userMsg.Content);

    var aiMsg = new ChatMessage
    {
        RegisteredProjectId = projectId,
        Role = "assistant",
        Content = aiReply,
        RelatedFilePath = userMsg.RelatedFilePath,
        CreatedAt = DateTime.UtcNow
    };
    db.ChatMessages.Add(aiMsg);
    await db.SaveChangesAsync();

    return Results.Ok(aiMsg);
});

app.MapPost("/api/quality-gate/check", async (Guid projectId, AppDbContext db) =>
{
    var project = await db.Projects
        .Include(p => p.Issues)
        .FirstOrDefaultAsync(p => p.Id == projectId);

    if (project == null) return Results.NotFound("Project không tồn tại.");

    int criticalCount = project.Issues.Count(i => i.Severity == "Critical" && i.Status == "Pending");
    int warningCount = project.Issues.Count(i => i.Severity == "Warning" && i.Status == "Pending");

    // Quy tắc: Trượt nếu còn bất kỳ lỗi Critical nào
    bool isPassed = criticalCount == 0;

    var result = new
    {
        Passed = isPassed,
        CriticalIssues = criticalCount,
        WarningIssues = warningCount,
        Message = isPassed 
            ? "✅ Quality Gate PASSED: Mã nguồn đạt tiêu chuẩn an toàn." 
            : $"❌ Quality Gate FAILED: Phát hiện {criticalCount} lỗi Critical chưa xử lý."
    };

    if (!isPassed)
    {
        return Results.Json(result, statusCode: 422); // Trả về HTTP 422 để báo lỗi cho CI/CD
    }

    return Results.Ok(result);
});

// Endpoint 2: AI Tạo gợi ý Code sửa lỗi
app.MapPost("/api/issues/{id:guid}/generate-fix", async (Guid id, AppDbContext db, GeminiService gemini) =>
{
    var issue = await db.Issues.FindAsync(id);
    if (issue == null) return Results.NotFound();

    string fixedCode = await gemini.GenerateFixCodeAsync(issue.CodeSnippet, issue.Message);
    return Results.Ok(new { originalCode = issue.CodeSnippet, fixedCode });
});

// Endpoint 3: Tạo 1-Click Pull Request lên GitHub
app.MapPost("/api/issues/{id:guid}/create-pr", async (Guid id, CreatePrDto req, AppDbContext db, GitHubService github) =>
{
    var issue = await db.Issues.FindAsync(id);
    if (issue == null) return Results.NotFound("Không tìm thấy Issue.");

    var project = await db.Projects.FindAsync(issue.RegisteredProjectId);
    if (project == null || string.IsNullOrEmpty(project.GitHubToken)) 
        return Results.BadRequest("Project chưa được cấu hình GitHub Access Token.");

    try
    {
        string prUrl = await github.CreateFixPullRequestAsync(
            project.GitHubToken,
            project.RepoOwner!,
            project.RepoName!,
            issue.FilePath,
            req.FixedCode,
            issue.Message
        );

        issue.Status = "Approved"; // Chuyển trạng thái lỗi thành Approved
        await db.SaveChangesAsync();

        return Results.Ok(new { prUrl });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Lỗi khi tạo Pull Request: {ex.Message}");
    }
});

app.MapPost("/api/projects/{id:guid}/scan", async (
    Guid id, 
    AppDbContext db, 
    GeminiService gemini, 
    SecretScannerService secretScanner, 
    DependencyScannerService depScanner) =>
{
    var project = await db.Projects.Include(p => p.Issues).FirstOrDefaultAsync(p => p.Id == id);
    if (project == null) return Results.NotFound("Dự án không tồn tại.");

    // Xóa danh sách lỗi cũ
    db.Issues.RemoveRange(project.Issues);

    var newIssues = new List<CodeIssue>();

    if (Directory.Exists(project.PathOrUrl))
    {
        // 1. Quét Secret Detection & AI Scan cho tất cả file .cs
        var csFiles = Directory.GetFiles(project.PathOrUrl, "*.cs", SearchOption.AllDirectories);
        foreach (var file in csFiles)
        {
            string content = await File.ReadAllTextAsync(file);
            string relativePath = Path.GetRelativePath(project.PathOrUrl, file);

            // a. Quét Secret (Regex)
            var secrets = secretScanner.ScanContent(relativePath, content);
            newIssues.AddRange(secrets);

            // b. Quét Code Quality & Security bằng Gemini AI (nếu chưa quá nhiều issue)
            var aiIssues = await gemini.AnalyzeCodeAsync(relativePath, content);
            newIssues.AddRange(aiIssues);
        }

        // 2. Quét Lỗ hổng thư viện NuGet (SCA) trên các file .csproj
        var csprojFiles = Directory.GetFiles(project.PathOrUrl, "*.csproj", SearchOption.AllDirectories);
        foreach (var csproj in csprojFiles)
        {
            var depIssues = await depScanner.ScanCsprojAsync(csproj);
            newIssues.AddRange(depIssues);
        }
    }

    project.Issues = newIssues;
    await db.SaveChangesAsync();

    return Results.Ok(new { Message = $"Đã hoàn tất quét! Phát hiện {newIssues.Count} vấn đề.", IssuesCount = newIssues.Count });
});

app.MapControllers();

app.Run();

public record CreatePrDto(string FixedCode); 