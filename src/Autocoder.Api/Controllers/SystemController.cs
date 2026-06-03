using Microsoft.AspNetCore.Mvc;

namespace Autocoder.Api.Controllers;

[ApiController]
public class SystemController : ControllerBase
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", "bin", "obj", ".vs", ".vscode", ".idea",
        "__pycache__", ".nuget", "packages", "vendor", "dist", "build",
        ".gradle", "target", "out", "coverage", ".next", ".svelte-kit",
    };

    [HttpGet("api/system/git-repos")]
    public ActionResult<List<GitRepoResult>> FindGitRepos([FromQuery] string? root, [FromQuery] int maxDepth = 4)
    {
        var searchRoot = root ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!Directory.Exists(searchRoot))
            return BadRequest($"Directory not found: {searchRoot}");

        var results = new List<GitRepoResult>();
        ScanForRepos(searchRoot, 0, maxDepth, results);

        return Ok(results.OrderBy(r => r.Path).Take(100).ToList());
    }

    private static void ScanForRepos(string dir, int depth, int maxDepth, List<GitRepoResult> results)
    {
        if (depth > maxDepth || results.Count >= 100) return;

        try
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                results.Add(new GitRepoResult(
                    Path.GetFileName(dir),
                    dir
                ));
                return; // don't recurse into repos
            }

            if (depth == maxDepth) return;

            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (name is null || name.StartsWith('.') || SkipDirs.Contains(name)) continue;
                ScanForRepos(sub, depth + 1, maxDepth, results);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
}

public record GitRepoResult(string Name, string Path);
