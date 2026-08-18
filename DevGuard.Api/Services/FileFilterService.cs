namespace DevGuard.Api.Services;

public class FileFilterService
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".vscode", "node_modules", "packages", "dist", "build"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".js", ".ts", ".py", ".java", ".json", ".xml", ".html", ".css", ".config"
    };

    public bool ShouldScanFile(string filePath)
    {
        var parts = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(part => IgnoredDirectories.Contains(part)))
        {
            return false;
        }

        var ext = Path.GetExtension(filePath);
        return AllowedExtensions.Contains(ext);
    }
}