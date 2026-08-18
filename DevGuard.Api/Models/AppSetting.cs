using System.ComponentModel.DataAnnotations;

namespace DevGuard.Api.Models;

public class AppSetting
{
    [Key]
    public string Key { get; set; } = string.Empty; // VD: "Gemini:ApiKey", "Gemini:Model"
    public string Value { get; set; } = string.Empty;
    public string Category { get; set; } = "General"; // VD: "AI", "System", "Security"
    public string Description { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}