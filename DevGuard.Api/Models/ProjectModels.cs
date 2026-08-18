namespace DevGuard.Api.Models;

public class RegisteredProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProjectSourceType { get; set; } = "Git"; // Git hoặc LocalFolder
    public string PathOrUrl { get; set; } = string.Empty;
    
    // Thêm các trường cho Giai đoạn 2:
    public string? GitHubToken { get; set; } // Personal Access Token (PAT)
    public string? RepoOwner { get; set; }   // ví dụ: "username" hoặc "org-name"
    public string? RepoName { get; set; }    // ví dụ: "my-repo"
    
    public List<CodeIssue> Issues { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CodeIssue
{
    public Guid Id { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public Guid RegisteredProjectId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Severity { get; set; } = "Warning";
    public string RuleCategory { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CodeSnippet { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Approved (Đúng), Rejected (False Positive)
    public int ScanHistoryId { get; set; }
    public ScanHistory? ScanHistory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ScanHistory
{
    public int Id { get; set; }
    public string RepositoryPath { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Processing";
    public int TotalIssues { get; set; }

    public List<CodeIssue> Issues { get; set; } = new();
}