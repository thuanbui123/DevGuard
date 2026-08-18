using Octokit;

namespace DevGuard.Api.Services;

public class GitHubService
{
    public async Task<string> CreateFixPullRequestAsync(
        string token, 
        string owner, 
        string repo, 
        string filePath, 
        string newCodeContent, 
        string issueTitle)
    {
        var github = new GitHubClient(new ProductHeaderValue("DevGuard-App"))
        {
            Credentials = new Credentials(token)
        };

        // 1. Lấy thông tin Repo & Branch mặc định (main hoặc master)
        var repository = await github.Repository.Get(owner, repo);
        var defaultBranch = await github.Git.Reference.Get(owner, repo, $"heads/{repository.DefaultBranch}");

        // 2. Tạo Branch mới cho bản vá
        string newBranchName = $"devguard-fix-{DateTime.UtcNow.Ticks}";
        await github.Git.Reference.Create(owner, repo, new NewReference($"refs/heads/{newBranchName}", defaultBranch.Object.Sha));

        // 3. Tìm file cần sửa trên Repo
        var existingFiles = await github.Repository.Content.GetAllContentsByRef(owner, repo, filePath, newBranchName);
        var targetFile = existingFiles.FirstOrDefault();

        if (targetFile == null) throw new Exception($"Không tìm thấy file {filePath} trên GitHub.");

        // 4. Cập nhật nội dung file trên Branch mới
        var updateFileRequest = new UpdateFileRequest($"[DevGuard Auto-Fix] {issueTitle}", newCodeContent, targetFile.Sha, newBranchName);
        await github.Repository.Content.UpdateFile(owner, repo, filePath, updateFileRequest);

        // 5. Tạo Pull Request
        var pr = new NewPullRequest(
            title: $"[DevGuard Auto-Fix] {issueTitle}",
            head: newBranchName,
            baseRef: repository.DefaultBranch)
        {
            Body = $"Pull Request này được tạo tự động bởi **DevGuard AI**.\n\n- **Tệp chỉnh sửa:** `{filePath}`\n- **Vấn đề:** {issueTitle}"
        };

        var createdPr = await github.PullRequest.Create(owner, repo, pr);
        return createdPr.HtmlUrl; // Trả về link PR vừa tạo
    }
}