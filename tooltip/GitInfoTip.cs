// Explorer infotip (hover tooltip) shell extension: shows git commit message, hash, author and file status.
// Plain .NET Framework 4 / C# 5, compiled with the csc.exe that ships with Windows. No third-party libraries.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ExplorerGitTip
{
    [ComImport, Guid("00021500-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IQueryInfo
    {
        [PreserveSig]
        int GetInfoTip(uint dwFlags, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszTip);
        [PreserveSig]
        int GetInfoFlags(out uint pdwFlags);
    }

    [ComVisible(true)]
    [Guid("6F1B2C3A-8D4E-4A57-9B61-3C2E5D7A9F10")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("ExplorerGitTip.GitInfoTip")]
    public class GitInfoTip : IQueryInfo, IPersistFile
    {
        private string path;

        // ---- IPersistFile ----
        public void GetClassID(out Guid pClassID) { pClassID = typeof(GitInfoTip).GUID; }
        public int IsDirty() { return 1; }
        public void Load(string pszFileName, int dwMode) { path = pszFileName; }
        public void Save(string pszFileName, bool fRemember) { }
        public void SaveCompleted(string pszFileName) { }
        public void GetCurFile(out string ppszFileName) { ppszFileName = path; }

        // ---- IQueryInfo ----
        public int GetInfoFlags(out uint pdwFlags) { pdwFlags = 0; return 0; }

        public int GetInfoTip(uint dwFlags, out string ppwszTip)
        {
            ppwszTip = null;
            try
            {
                string tip = Build(path);
                if (tip == null) return unchecked((int)0x80004005); // E_FAIL -> Explorer shows its default tip
                ppwszTip = tip;
                return 0;
            }
            catch { return unchecked((int)0x80004005); }
        }

        // ---------------- git ----------------
        private static string Build(string p)
        {
            if (string.IsNullOrEmpty(p)) return null;
            bool isDir = Directory.Exists(p);
            string dir = isDir ? p : Path.GetDirectoryName(p);
            if (dir == null) return null;
            string name = Path.GetFileName(p);
            if (name.Length == 0 || name == ".git" || p.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0) return null;

            string root = Git(dir, "rev-parse --show-toplevel");
            if (string.IsNullOrEmpty(root)) return null; // not a git repo: leave default tooltip

            string rel = "\"" + p.Replace("\"", "") + "\"";
            string status = DescribeStatus(Git(dir, "-c core.quotepath=off status --porcelain=v1 --untracked-files=all -- " + rel), isDir);
            string log = Git(dir, "log -1 --date=format:%Y-%m-%d_%H:%M --format=%h%x1f%an%x1f%ad%x1f%s -- " + rel);

            StringBuilder sb = new StringBuilder();
            sb.Append("Git status: ").Append(status);
            if (!string.IsNullOrEmpty(log))
            {
                string[] f = log.Trim().Split('\x1f');
                if (f.Length == 4)
                {
                    sb.Append("\nCommit: ").Append(f[0]);
                    sb.Append("\nMessage: ").Append(f[3]);
                    sb.Append("\nAuthor: ").Append(f[1]);
                    sb.Append("\nDate: ").Append(f[2].Replace('_', ' '));
                }
            }
            else sb.Append("\n(no commit yet)");
            return sb.ToString();
        }

        private static string DescribeStatus(string porcelain, bool isDir)
        {
            if (string.IsNullOrEmpty(porcelain)) return "Committed (clean)";
            string[] lines = porcelain.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (isDir || lines.Length > 1) return "Changed (" + lines.Length + " item" + (lines.Length == 1 ? "" : "s") + ")";
            string l = lines[0];
            if (l.Length < 2) return "Changed";
            char x = l[0], y = l[1];
            if (x == '?') return "Added (untracked)";
            if (x == '!') return "Ignored";
            bool staged = x != ' ', work = y != ' ';
            if (staged && work) return "Staged + Modified";
            if (staged) return x == 'A' ? "Added (staged)" : "Staged";
            return "Modified";
        }

        // ---------------- cache + process ----------------
        private static readonly Dictionary<string, KeyValuePair<DateTime, string>> cache = new Dictionary<string, KeyValuePair<DateTime, string>>();
        private static readonly object gate = new object();

        private static string Git(string dir, string args)
        {
            string key = dir + "|" + args;
            lock (gate)
            {
                KeyValuePair<DateTime, string> hit;
                if (cache.TryGetValue(key, out hit) && (DateTime.UtcNow - hit.Key).TotalSeconds < 3) return hit.Value;
            }
            string result = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("git", args);
                psi.WorkingDirectory = dir;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                using (Process pr = Process.Start(psi))
                {
                    pr.ErrorDataReceived += delegate { };
                    pr.BeginErrorReadLine();
                    System.Threading.Tasks.Task<string> read = pr.StandardOutput.ReadToEndAsync();
                    if (!pr.WaitForExit(2500)) { try { pr.Kill(); } catch { } }
                    else if (pr.ExitCode == 0 && read.Wait(500)) result = read.Result.TrimEnd('\r', '\n');
                }
            }
            catch { }
            lock (gate)
            {
                if (cache.Count > 500) cache.Clear();
                cache[key] = new KeyValuePair<DateTime, string>(DateTime.UtcNow, result);
            }
            return result;
        }
    }
}
