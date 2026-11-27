// Windows property handler that exposes git info as real Explorer "Details" columns.
// Properties: Git Status, Git Commit (hash), Git Message, Git Author, Git Date.
// .NET Framework 4 / C# 5, x64. Compiled with the csc.exe that ships with Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ExplorerGitColumns
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PROPVARIANT
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr p;
    }

    [ComImport, Guid("b7d14566-0509-4cce-a71f-0a554233bd9b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IInitializeWithFile
    {
        [PreserveSig] int Initialize([MarshalAs(UnmanagedType.LPWStr)] string pszFilePath, uint grfMode);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint cProps);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("c8e2d566-186e-4d49-bf41-6909ead56acc"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStoreCapabilities
    {
        [PreserveSig] int IsPropertyWritable(ref PROPERTYKEY key);
    }

    [ComVisible(true)]
    [Guid("A3D5E7F1-2B4C-4D6E-8F90-1A2B3C4D5E6F")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("ExplorerGitColumns.GitPropertyHandler")]
    public class GitPropertyHandler : IInitializeWithFile, IPropertyStore, IPropertyStoreCapabilities
    {
        // Must match ExplorerGit.propdesc
        public static readonly Guid Fmtid = new Guid("5B1C7E22-9D3A-4F68-A0B4-6E2D8C1F7A93");
        const uint PidStatus = 2, PidHash = 3, PidMessage = 4, PidAuthor = 5, PidDate = 6;
        static readonly uint[] Pids = { PidStatus, PidHash, PidMessage, PidAuthor, PidDate };

        const int S_OK = 0;
        const int S_FALSE = 1;
        const int STG_E_ACCESSDENIED = unchecked((int)0x80030005);
        const ushort VT_EMPTY = 0, VT_LPWSTR = 31;

        string path;
        string[] values; // indexed like Pids; computed lazily

        public int Initialize(string pszFilePath, uint grfMode) { path = pszFilePath; values = null; return S_OK; }

        public int GetCount(out uint cProps) { cProps = (uint)Pids.Length; return S_OK; }

        public int GetAt(uint iProp, out PROPERTYKEY pkey)
        {
            pkey = new PROPERTYKEY();
            if (iProp >= Pids.Length) return unchecked((int)0x80070057); // E_INVALIDARG
            pkey.fmtid = Fmtid; pkey.pid = Pids[iProp];
            return S_OK;
        }

        public int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv)
        {
            pv = new PROPVARIANT(); pv.vt = VT_EMPTY;
            if (key.fmtid != Fmtid) return S_OK;
            int idx = Array.IndexOf(Pids, key.pid);
            if (idx < 0) return S_OK;
            try
            {
                if (values == null) values = GitInfo.Get(path);
                string v = values[idx];
                if (string.IsNullOrEmpty(v)) return S_OK;
                pv.vt = VT_LPWSTR;
                pv.p = Marshal.StringToCoTaskMemUni(v);
            }
            catch { }
            return S_OK;
        }

        public int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv) { return STG_E_ACCESSDENIED; }
        public int Commit() { return S_OK; }
        public int IsPropertyWritable(ref PROPERTYKEY key) { return S_FALSE; }
    }

    internal static class GitInfo
    {
        // [status, hash, message, author, date]
        public static string[] Get(string file)
        {
            string[] empty = new string[5];
            if (string.IsNullOrEmpty(file)) return empty;
            string dir = Path.GetDirectoryName(file);
            if (dir == null || file.IndexOf("\\.git\\", StringComparison.OrdinalIgnoreCase) >= 0) return empty;

            string key = file.ToLowerInvariant();
            lock (gate)
            {
                Entry e;
                if (cache.TryGetValue(key, out e) && (DateTime.UtcNow - e.When).TotalSeconds < 20) return e.Vals;
            }

            string[] r = new string[5];
            string quoted = "\"" + file.Replace("\"", "") + "\"";
            string top = Git(dir, "rev-parse --show-toplevel");
            if (!string.IsNullOrEmpty(top))
            {
                string st = Git(dir, "-c core.quotepath=off status --porcelain=v1 --untracked-files=all -- " + quoted);
                r[0] = Describe(st);
                string log = Git(dir, "log -1 --date=format:%Y-%m-%d_%H:%M --format=%h%x1f%an%x1f%ad%x1f%s -- " + quoted);
                if (!string.IsNullOrEmpty(log))
                {
                    string[] f = log.Trim().Split('\x1f');
                    if (f.Length == 4) { r[1] = f[0]; r[2] = f[3]; r[3] = f[1]; r[4] = f[2].Replace('_', ' '); }
                }
                else r[2] = "(not committed yet)";
            }
            lock (gate)
            {
                if (cache.Count > 2000) cache.Clear();
                cache[key] = new Entry { When = DateTime.UtcNow, Vals = r };
            }
            return r;
        }

        static string Describe(string porcelain)
        {
            if (string.IsNullOrEmpty(porcelain)) return "Committed";
            string[] lines = porcelain.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
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

        class Entry { public DateTime When; public string[] Vals; }
        static readonly Dictionary<string, Entry> cache = new Dictionary<string, Entry>();
        static readonly object gate = new object();

        static string Git(string dir, string args)
        {
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
                    if (!pr.WaitForExit(4000)) { try { pr.Kill(); } catch { } return ""; }
                    if (pr.ExitCode == 0 && read.Wait(1000)) return read.Result.TrimEnd('\r', '\n');
                }
            }
            catch { }
            return "";
        }
    }
}
