namespace DevGuard.Api.Models;

public class GeminiReviewResponse
{
    public List<GeminiIssueDto> Issues { get; set; } = new();
}

public class GeminiIssueDto
{
    public int LineNumber { get; set; }
    public string Severity { get; set; } = "Warning";
    public string RuleCategory { get; set; } = "Security";
    public string Message { get; set; } = string.Empty;
    public string CodeSnippet { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
}