using System.ComponentModel.DataAnnotations;

namespace DevGuard.Api.Models;

public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RegisteredProjectId { get; set; }
    public string Role { get; set; } = "user"; // "user" hoặc "assistant"
    public string Content { get; set; } = string.Empty;
    public string? RelatedFilePath { get; set; } // File code đang thảo luận (nếu có)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}