using System.Text;
using System.Text.Json;
using DevGuard.Api.Data;
using DevGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevGuard.Api.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;

    public GeminiService(HttpClient httpClient, AppDbContext db)
    {
        _httpClient = httpClient;
        _db = db;
    }

    public async Task<List<CodeIssue>> ReviewCodeWithAiAsync(Guid projectId, string filePath, string codeContent)
    {
        // 1. Đọc API Key & Model từ AppSettings
        var apiKeySetting = await _db.AppSettings.FindAsync("Gemini:ApiKey");
        var modelSetting = await _db.AppSettings.FindAsync("Gemini:Model");
        string apiKey = apiKeySetting?.Value ?? string.Empty;
        string model = modelSetting?.Value ?? "gemini-1.5-flash";

        if (string.IsNullOrWhiteSpace(apiKey)) return new List<CodeIssue>();

        // 2. Lấy dữ liệu phản hồi quá khứ từ Database để làm ví dụ học (Few-Shot Learning)
        var approvedExamples = await _db.Issues
            .Where(i => i.Status == "Approved")
            .OrderByDescending(i => i.CreatedAt)
            .Take(3)
            .Select(i => $"- Lỗi ĐÚNG nên báo: Snippet '{i.CodeSnippet}' -> {i.Message}")
            .ToListAsync();

        var rejectedExamples = await _db.Issues
            .Where(i => i.Status == "Rejected")
            .OrderByDescending(i => i.CreatedAt)
            .Take(3)
            .Select(i => $"- Lỗi SAI (False Positive) KHÔNG ĐƯỢC báo lại: Snippet '{i.CodeSnippet}' -> Lý do: {i.Message}")
            .ToListAsync();

        string fewShotContext = "";
        if (approvedExamples.Any() || rejectedExamples.Any())
        {
            fewShotContext = "\n\nDỰA TRÊN LỊCH SỬ FEEDBACK CỦA DEVELOPER TRƯỚC ĐÂY:\n" +
                            string.Join("\n", approvedExamples) + "\n" +
                            string.Join("\n", rejectedExamples);
        }

        // 3. Xây dựng Dynamic System Prompt kết hợp kinh nghiệm đã học
        var systemPrompt = $@"Bạn là Senior Security Auditor & Code Reviewer.
    Hãy phân tích mã nguồn sau và tìm các lỗi bảo mật, hiệu năng, hoặc Code Smells.
    {fewShotContext}

    BẮT BUỘC trả về kết quả theo chuẩn JSON Schema sau, không thêm markdown hay text bên ngoài:
    {{
    ""issues"": [
        {{
        ""lineNumber"": 10,
        ""severity"": ""Critical"",
        ""ruleCategory"": ""Security"",
        ""message"": ""Mô tả lỗi ngắn gọn"",
        ""codeSnippet"": ""Đoạn code bị lỗi"",
        ""suggestedFix"": ""Đoạn code khuyến nghị sửa""
        }}
    ]
    }}";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { text = systemPrompt },
                        new { text = $"File: {filePath}\nCode:\n```{codeContent}```" }
                    }
                }
            },
            generationConfig = new
            {
                response_mime_type = "application/json",
                temperature = 0.2
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, jsonContent);
        if (!response.IsSuccessStatusCode) return new List<CodeIssue>();

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        
        var rawJson = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrEmpty(rawJson)) return new List<CodeIssue>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var aiResult = JsonSerializer.Deserialize<GeminiReviewResponse>(rawJson, options);

        if (aiResult?.Issues == null) return new List<CodeIssue>();

        return aiResult.Issues.Select(i => new CodeIssue
        {
            RegisteredProjectId = projectId,
            FilePath = filePath,
            LineNumber = i.LineNumber,
            Severity = i.Severity,
            RuleCategory = $"AI: {i.RuleCategory}",
            Message = string.IsNullOrEmpty(i.SuggestedFix) ? i.Message : $"{i.Message} (Đề xuất sửa: {i.SuggestedFix})",
            CodeSnippet = i.CodeSnippet,
            Status = "Pending"
        }).ToList();
    }

    public async Task<string> ChatWithProjectContextAsync(List<ChatMessage> history, string userPrompt)
    {
        var apiKeySetting = await _db.AppSettings.FindAsync("Gemini:ApiKey");
        var modelSetting = await _db.AppSettings.FindAsync("Gemini:Model");
        string apiKey = apiKeySetting?.Value ?? string.Empty;
        string model = modelSetting?.Value ?? "gemini-1.5-flash";

        if (string.IsNullOrWhiteSpace(apiKey)) return "Chưa cấu hình API Key.";

        var contents = history.Select(h => new
        {
            role = h.Role == "user" ? "user" : "model",
            parts = new[] { new { text = h.Content } }
        }).ToList();

        var requestBody = new { contents };
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, jsonContent);
        if (!response.IsSuccessStatusCode) return "Lỗi khi kết nối tới AI.";

        var responseString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseString);
        
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "Không có câu trả lời.";
    }

    public async Task<string> GenerateFixCodeAsync(string originalCode, string issueMessage)
    {
        var apiKeySetting = await _db.AppSettings.FindAsync("Gemini:ApiKey");
        var modelSetting = await _db.AppSettings.FindAsync("Gemini:Model");
        string apiKey = apiKeySetting?.Value ?? string.Empty;
        string model = modelSetting?.Value ?? "gemini-1.5-flash";

        if (string.IsNullOrWhiteSpace(apiKey)) return originalCode;

        var prompt = $@"Bạn là một Senior Software Developer.
    Dưới đây là đoạn code bị lỗi:
    ```csharp
    {originalCode}
    Lỗi được ghi nhận: {issueMessage}

    Hãy sửa lại đoạn code trên sao cho an toàn, tối ưu và tuân thủ clean code.
    BẮT BUỘC CHỈ TRẢ VỀ ĐOẠN CODE ĐÃ SỬA (không thêm giải thích, không bọc markdown):";
    var requestBody = new
    {
        contents = new[]
        {
            new { parts = new[] { new { text = prompt } } }
        }
    };

    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
    var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

    var response = await _httpClient.PostAsync(url, jsonContent);
    if (!response.IsSuccessStatusCode) return originalCode;

    var responseString = await response.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(responseString);
    var fixText = doc.RootElement
        .GetProperty("candidates")[0]
        .GetProperty("content")
        .GetProperty("parts")[0]
        .GetProperty("text")
        .GetString() ?? originalCode;

    // Lọc bỏ markdown codeblock nếu AI lỡ trả về (```csharp ... ```)
    return fixText.Replace("```csharp", "").Replace("```", "").Trim();
    }

    public async Task<List<CodeIssue>> AnalyzeCodeAsync(string relativePath, string content)
    {
        var issues = new List<CodeIssue>();

        var apiKeySetting = await _db.AppSettings.FindAsync("Gemini:ApiKey");
        var modelSetting = await _db.AppSettings.FindAsync("Gemini:Model");
        string apiKey = apiKeySetting?.Value ?? string.Empty;
        string model = modelSetting?.Value ?? "gemini-1.5-flash";

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(content)) 
            return issues;

        var prompt = $@"Bạn là một chuyên gia Security & Code Review. Hãy phân tích file code '{relativePath}' dưới đây:

        ```csharp
        {content}
        Hãy tìm các lỗi bảo mật, code smell, hoặc vi phạm clean code.
        BẮT BUỘC trả về duy nhất một chuỗi JSON Array theo định dạng sau (không bọc markdown, không thêm giải thích):
        [
        {{
        ""lineNumber"": 12,
        ""severity"": ""Critical"",
        ""ruleId"": ""SEC-001"",
        ""message"": ""Mô tả lỗi ngắn gọn"",
        ""codeSnippet"": ""đoạn code bị lỗi""
        }}
        ]";
        try
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, jsonContent);
                if (!response.IsSuccessStatusCode) return issues;

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                
                var aiResponseText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "[]";

                // Làm sạch response nếu Gemini lỡ bọc ```json ... ```
                aiResponseText = aiResponseText.Replace("```json", "").Replace("```", "").Trim();

                using var issuesDoc = JsonDocument.Parse(aiResponseText);
                foreach (var element in issuesDoc.RootElement.EnumerateArray())
                {
                    issues.Add(new CodeIssue
                    {
                        FilePath = relativePath,
                        LineNumber = element.TryGetProperty("lineNumber", out var ln) ? ln.GetInt32() : 1,
                        Severity = element.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "Warning" : "Warning",
                        RuleId = element.TryGetProperty("ruleId", out var rid) ? rid.GetString() ?? "AI-REVIEW" : "AI-REVIEW",
                        Message = element.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "",
                        CodeSnippet = element.TryGetProperty("codeSnippet", out var cs) ? cs.GetString() ?? "" : "",
                        Status = "Pending"
                    });
                }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DevGuard Gemini Error] File: {relativePath} - Exception: {ex.Message}");

            // Tạo một issue cảnh báo hệ thống để hiển thị cho người dùng biết AI analysis gặp sự cố
            issues.Add(new CodeIssue
            {
                FilePath = relativePath,
                LineNumber = 1,
                Severity = "Warning",
                RuleId = "SYSTEM-AI-ERROR",
                Message = $"Không thể phân tích file qua Gemini AI: {ex.Message}",
                CodeSnippet = "",
                Status = "Pending"
            });
        }

        return issues;
    }
}