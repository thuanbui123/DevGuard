using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DevGuard.Api.Models;

namespace DevGuard.Api.Services;

public class DependencyScannerService
{
    private readonly HttpClient _httpClient;

    public DependencyScannerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<CodeIssue>> ScanCsprojAsync(string csprojFilePath)
    {
        var issues = new List<CodeIssue>();
        if (!File.Exists(csprojFilePath)) return issues;

        // 1. Đọc file .csproj và trích xuất các PackageReference
        var xml = await File.ReadAllTextAsync(csprojFilePath);
        var doc = XDocument.Parse(xml);

        var packages = doc.Descendants("PackageReference")
            .Select(p => new
            {
                Name = p.Attribute("Include")?.Value,
                Version = p.Attribute("Version")?.Value
            })
            .Where(p => !string.IsNullOrEmpty(p.Name) && !string.IsNullOrEmpty(p.Version))
            .ToList();

        // 2. Tra cứu lỗ hổng qua OSV.dev API (Google Open Source Vulnerabilities)
        foreach (var pkg in packages)
        {
            var requestBody = new
            {
                package = new { name = pkg.Name, ecosystem = "NuGet" },
                version = pkg.Version
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.osv.dev/v1/query", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseString);

                if (jsonDoc.RootElement.TryGetProperty("vulns", out var vulns) && vulns.GetArrayLength() > 0)
                {
                    foreach (var vuln in vulns.EnumerateArray())
                    {
                        string id = vuln.GetProperty("id").GetString() ?? "CVE-UNKNOWN";
                        string summary = vuln.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "Lỗ hổng bảo mật phụ thuộc.";

                        issues.Add(new CodeIssue
                        {
                            FilePath = Path.GetFileName(csprojFilePath),
                            LineNumber = 1,
                            Severity = "Critical",
                            RuleId = id,
                            Message = $"📦 Thư viện [{pkg.Name} v{pkg.Version}] dính lỗ hổng {id}: {summary}",
                            CodeSnippet = $"<PackageReference Include=\"{pkg.Name}\" Version=\"{pkg.Version}\" />",
                            Status = "Pending"
                        });
                    }
                }
            }
        }

        return issues;
    }
}