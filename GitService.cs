using System.Diagnostics;
using System.Text;

namespace ExplorerGit;

public record CommitInfo(string Hash, string Author, string Date, string Message);

public static class GitService
{
    public static string Run(string dir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8
        };
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("core.quotepath=off");
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var errTask = p.StandardError.ReadToEndAsync();
        var o = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return p.ExitCode == 0 ? o : "";
    }

    public static string? RepoRoot(string dir)
    {
        var r = Run(dir, "rev-parse", "--show-toplevel").Trim();
        return r.Length == 0 ? null : Path.GetFullPath(r);
    }

    /// Map of repo-relative path (forward slashes) -> human status.
    public static Dictionary<string, string> Status(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = Run(root, "status", "--porcelain=v1", "-z", "--untracked-files=all").Split('\0', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < entries.Length; i++)
        {
            var e = entries[i];
            if (e.Length < 4) continue;
            char x = e[0], y = e[1];
            var path = e[3..];
            if (x is 'R' or 'C') i++; // skip original-path entry
            string s;
            if (x == '?') s = "Added (untracked)";
            else if (x == '!') s = "Ignored";
            else
            {
                bool staged = x != ' ', work = y != ' ';
                var stagedTxt = x == 'A' ? "Added (staged)" : "Staged";
                s = staged && work ? "Staged + Modified" : staged ? stagedTxt : "Modified";
            }
            map[path] = s;
        }
        return map;
    }

    public static CommitInfo? LastCommit(string root, string relPath)
    {
        var o = Run(root, "log", "-1", "--date=format:%Y-%m-%d %H:%M", "--format=%h%x1f%an%x1f%ad%x1f%s", "--", relPath).Trim();
        var p = o.Split('\x1f');
        return p.Length == 4 ? new CommitInfo(p[0], p[1], p[2], p[3]) : null;
    }

    public static string Branch(string root) => Run(root, "rev-parse", "--abbrev-ref", "HEAD").Trim();

    /// Status for a path: exact match, or aggregate for folders.
    public static string StatusFor(Dictionary<string, string> map, string rel, bool isDir)
    {
        if (map.TryGetValue(rel, out var s)) return s;
        if (isDir)
        {
            var prefix = rel.Length == 0 ? "" : rel + "/";
            var n = map.Count(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && kv.Value != "Ignored");
            if (n > 0) return $"Changed ({n})";
        }
        return "Committed";
    }
}
