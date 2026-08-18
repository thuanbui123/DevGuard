using DevGuard.Api.Models;
using Microsoft.EntityFrameworkCore;
using DevGuard.Api.Data;  

namespace DevGuard.Api.Services;

public class ScanPipelineService
{
    private readonly FileFilterService _filterService;
    private readonly SecretScannerService _secretScanner;
    private readonly GeminiService _geminiService;
    private readonly AppDbContext _db;
    private readonly SemaphoreSlim _semaphore = new(3, 3); // Giới hạn tối đa 3 request Gemini cùng lúc

    public ScanPipelineService(
        FileFilterService filterService,
        SecretScannerService secretScanner,
        GeminiService geminiService,
        AppDbContext db)
    {
        _filterService = filterService;
        _secretScanner = secretScanner;
        _geminiService = geminiService;
        _db = db;
    }

    public async Task<ScanHistory> ExecuteScanAsync(string targetFolderPath)
    {
        var scanHistory = new ScanHistory
        {
            RepositoryPath = targetFolderPath,
            ScannedAt = DateTime.UtcNow,
            Status = "Processing"
        };

        _db.ScanHistories.Add(scanHistory);
        await _db.SaveChangesAsync();

        var allIssues = new List<CodeIssue>();
        var allFiles = Directory.GetFiles(targetFolderPath, "*.*", SearchOption.AllDirectories);

        var validFiles = allFiles
            .Where(f => _filterService.ShouldScanFile(f))
            .ToList();

        // Task danh sách gửi cho Gemini AI
        var aiTasks = new List<Task<List<CodeIssue>>>();

        foreach (var file in validFiles)
        {
            var relativePath = Path.GetRelativePath(targetFolderPath, file);
            var content = await File.ReadAllTextAsync(file);

            // 1. Quét Secret Regex Cục Bộ (Nhanh)
            var secretIssues = _secretScanner.ScanFile(relativePath, content);
            allIssues.AddRange(secretIssues);

            // 2. Đưa vào hàng đợi Gemini AI Scan (Giới hạn luồng với SemaphoreSlim)
            aiTasks.Add(Task.Run(async () =>
            {
                await _semaphore.WaitAsync();
                try
                {
                    return await _geminiService.AnalyzeCodeAsync(relativePath, content);
                }
                finally
                {
                    _semaphore.Release();
                }
            }));
        }

        // Chờ tất cả AI tasks hoàn thành
        var aiResults = await Task.WhenAll(aiTasks);
        foreach (var issues in aiResults)
        {
            allIssues.AddRange(issues);
        }

        // Gán ScanHistoryId cho các issue tìm được
        foreach (var issue in allIssues)
        {
            issue.ScanHistoryId = scanHistory.Id;
            _db.Issues.Add(issue);
        }

        // Cập nhật trạng thái lượt quét
        scanHistory.TotalIssues = allIssues.Count;
        scanHistory.Status = "Completed";
        await _db.SaveChangesAsync();

        return scanHistory;
    }
}