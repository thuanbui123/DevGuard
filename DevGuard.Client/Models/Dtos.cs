namespace DevGuard.Client.Models;

public class RegisteredProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProjectSourceType { get; set; } = "Git";
    public string PathOrUrl { get; set; } = string.Empty;

    // Bổ sung cho Giai đoạn 2:
    public string? GitHubToken { get; set; }
    public string? RepoOwner { get; set; }
    public string? RepoName { get; set; }

    public List<CodeIssueDto> Issues { get; set; } = new();
}

public class CodeIssueDto
{
    public Guid Id { get; set; }
    public string RuleId { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Severity { get; set; } = "Warning";
    public string RuleCategory { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CodeSnippet { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public class AppSettingDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? RelatedFilePath { get; set; }
    public DateTime CreatedAt { get; set; }
}