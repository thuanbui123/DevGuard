using System.Text.RegularExpressions;
using DevGuard.Api.Models;

namespace DevGuard.Api.Services;

public class SecretScannerService
{
    // Tập hợp các biểu thức chính quy (Regex) để phát hiện Secret phổ biến
    private static readonly List<(string Type, string Pattern, string Severity)> Rules = new()
    {
        ("AWS Access Key ID", @"\b(AKIA|ASIA)[0-9A-Z]{16}\b", "Critical"),
        ("Generic API Key / Secret", @"(?i)(api_key|apikey|secret_key|app_secret)\s*[:=]\s*['""][a-zA-Z0-9_\-]{16,}['""]", "Critical"),
        ("Database Connection String", @"(?i)(Server|Data Source)=.*;.*(User Id|UID)=.*;.*(Password|PWD)=.*", "Critical"),
        ("JWT Token", @"eyJ[A-Za-z0-9-_=]+\.[A-Za-z0-9-_=]+\.?[A-Za-z0-9-_.+/=]*", "Warning"),
        ("Private Key Header", @"-----BEGIN (RSA|EC|DSA|OPENSSH|PRIVATE) KEY-----", "Critical")
    };

    private static readonly Regex SecretRegex = new(
        @"(Password|Secret|ApiKey|ConnectionString)\s*=\s*[""'].+[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public List<CodeIssue> ScanFile(string relativePath, string content)
    {
        var issues = new List<CodeIssue>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (SecretRegex.IsMatch(line))
            {
                issues.Add(new CodeIssue
                {
                    FilePath = relativePath,
                    LineNumber = i + 1,
                    Severity = "Critical",
                    RuleId = "SEC-001",
                    Message = $"Phát hiện thông tin nhạy cảm có thể bị hardcode: \"{line}\"",
                    CodeSnippet = line,
                    Status = "Pending"
                });
            }
        }

        return issues;
    }

    public List<CodeIssue> ScanContent(string filePath, string fileContent)
    {
        var issues = new List<CodeIssue>();
        var lines = fileContent.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            foreach (var (type, pattern, severity) in Rules)
            {
                if (Regex.IsMatch(line, pattern))
                {
                    issues.Add(new CodeIssue
                    {
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Severity = severity,
                        RuleId = "SEC-SECRET-001",
                        Message = $"🚨 Phát hiện nguy cơ lộ bí mật: {type}",
                        CodeSnippet = line.Trim(),
                        Status = "Pending"
                    });
                }
            }
        }

        return issues;
    }
}