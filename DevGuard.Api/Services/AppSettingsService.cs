using DevGuard.Api.Data;
using DevGuard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DevGuard.Api.Services;

public class AppSettingsService
{
    private readonly AppDbContext _db;

    public AppSettingsService(AppDbContext db)
    {
        _db = db;
    }

    // Lấy giá trị cấu hình theo Key
    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        var setting = await _db.AppSettings.FindAsync(key);
        return setting?.Value ?? defaultValue;
    }

    // Thêm hoặc Cập nhật giá trị cấu hình
    public async Task SetSettingAsync(string key, string value, string category = "General", string description = "")
    {
        var setting = await _db.AppSettings.FindAsync(key);
        if (setting == null)
        {
            _db.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = value,
                Category = category,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    // Khởi tạo các Key mặc định khi DB rỗng
    public async Task SeedDefaultSettingsAsync()
    {
        if (!await _db.AppSettings.AnyAsync())
        {
            _db.AppSettings.AddRange(
                new AppSetting { Key = "Gemini:ApiKey", Value = "YOUR_GEMINI_API_KEY_HERE", Category = "AI", Description = "API Key cho Google Gemini LLM" },
                new AppSetting { Key = "Gemini:Model", Value = "gemini-1.5-flash", Category = "AI", Description = "Tên Model Gemini" },
                new AppSetting { Key = "Scanner:MaxFilesPerScan", Value = "5", Category = "System", Description = "Số file tối đa mỗi lượt quét" }
            );
            await _db.SaveChangesAsync();
        }
    }
}