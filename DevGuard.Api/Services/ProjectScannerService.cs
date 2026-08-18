using DevGuard.Api.Models;
using Esprima;
using Esprima.Ast;
using LibGit2Sharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DevGuard.Api.Services;

public class ProjectScannerService
{
    private readonly GeminiService _geminiService;

    public ProjectScannerService(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }
    
    public string FetchProjectPath(RegisteredProject project)
    {
        if (project.ProjectSourceType.Equals("LocalFolder", StringComparison.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(project.PathOrUrl))
                throw new DirectoryNotFoundException($"Không tìm thấy đường dẫn: {project.PathOrUrl}");
            return project.PathOrUrl;
        }

        string repoName = Path.GetFileNameWithoutExtension(project.PathOrUrl);
        string tempFolder = Path.Combine(Path.GetTempPath(), "devguard_repos", repoName);

        if (Directory.Exists(tempFolder))
        {
            using var repo = new Repository(tempFolder);
            Commands.Pull(repo, new Signature("DevGuard", "bot@devguard.com", DateTimeOffset.Now), new PullOptions());
        }
        else
        {
            Repository.Clone(project.PathOrUrl, tempFolder);
        }

        return tempFolder;
    }

    public async Task<List<CodeIssue>> ScanDirectoryAsync(Guid projectId, string targetFolder)
    {
        var issues = new List<CodeIssue>();
        var ignoreDirs = new[] { "bin", "obj", "node_modules", ".git", ".vs" };

        var csFiles = Directory.GetFiles(targetFolder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ignoreDirs.Any(d => f.Contains(Path.DirectorySeparatorChar + d + Path.DirectorySeparatorChar)))
            .Take(5); // Giới hạn 5 file để tối ưu tốc độ và quota API Gemini

        foreach (var file in csFiles)
        {
            string relativePath = Path.GetRelativePath(targetFolder, file);
            string codeContent = await File.ReadAllTextAsync(file);

            // Gọi AI Gemini để review mã nguồn
            var aiIssues = await _geminiService.ReviewCodeWithAiAsync(projectId, relativePath, codeContent);
            issues.AddRange(aiIssues);
        }

        return issues;
    }

    public List<CodeIssue> ScanDirectory(Guid projectId, string targetFolder)
    {
        var issues = new List<CodeIssue>();
        var ignoreDirs = new[] { "bin", "obj", "node_modules", ".git", ".vs" };

        var csFiles = Directory.GetFiles(targetFolder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !ignoreDirs.Any(d => f.Contains(Path.DirectorySeparatorChar + d + Path.DirectorySeparatorChar)));

        foreach (var file in csFiles)
        {
            issues.AddRange(AnalyzeCSharpFile(projectId, file, targetFolder));
        }

        return issues;
    }

    private List<CodeIssue> AnalyzeCSharpFile(Guid projectId, string filePath, string rootFolder)
    {
        var issues = new List<CodeIssue>();
        string code = File.ReadAllText(filePath);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
        SyntaxNode root = tree.GetRoot();

        var loopNodes = root.DescendantNodes().Where(n => 
            n.IsKind(SyntaxKind.ForEachStatement) || 
            n.IsKind(SyntaxKind.ForStatement) || 
            n.IsKind(SyntaxKind.WhileStatement));

        foreach (var loop in loopNodes)
        {
            var linqCalls = loop.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.ToString().Contains(".Where") || inv.ToString().Contains(".Select") || inv.ToString().Contains(".FirstOrDefault"));

            foreach (var linq in linqCalls)
            {
                var lineSpan = linq.GetLocation().GetLineSpan();
                issues.Add(new CodeIssue
                {
                    RegisteredProjectId = projectId,
                    FilePath = Path.GetRelativePath(rootFolder, filePath),
                    LineNumber = lineSpan.StartLinePosition.Line + 1,
                    Severity = "Warning",
                    RuleCategory = "Performance",
                    Message = "Phát hiện LINQ trong vòng lặp. Nguy cơ gây truy vấn N+1.",
                    CodeSnippet = linq.ToString()
                });
            }
        }

        return issues;
    }
}