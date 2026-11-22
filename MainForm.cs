using System.Diagnostics;

namespace ExplorerGit;

public class MainForm : Form
{
    readonly TextBox pathBox = new() { Dock = DockStyle.Fill };
    readonly Label info = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    readonly ListView list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true };
    string current = "";
    int version;

    public MainForm(string startPath)
    {
        Text = "Explorer Git"; Width = 1200; Height = 700;
        foreach (var (n, w) in new[] { ("Name", 260), ("Status", 130), ("Commit message", 380), ("Hash", 70), ("Author", 130), ("Date", 120) })
            list.Columns.Add(n, w);

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 62, ColumnCount = 3, RowCount = 2 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        var up = new Button { Text = "Up", Dock = DockStyle.Fill };
        var refresh = new Button { Text = "Refresh" };
        var browse = new Button { Text = "Browse..." };
        up.Click += (_, _) => { var d = Directory.GetParent(current); if (d != null) Navigate(d.FullName); };
        refresh.Click += (_, _) => Navigate(current);
        browse.Click += (_, _) => { using var f = new FolderBrowserDialog { SelectedPath = current }; if (f.ShowDialog() == DialogResult.OK) Navigate(f.SelectedPath); };
        pathBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { Navigate(pathBox.Text); e.SuppressKeyPress = true; } };
        var btns = new FlowLayoutPanel { Dock = DockStyle.Fill };
        btns.Controls.Add(refresh); btns.Controls.Add(browse);
        top.Controls.Add(up, 0, 0); top.Controls.Add(pathBox, 1, 0); top.Controls.Add(btns, 2, 0);
        top.Controls.Add(info, 1, 1); top.SetColumnSpan(info, 2);

        list.DoubleClick += (_, _) =>
        {
            if (list.SelectedItems.Count == 0) return;
            var full = (string)list.SelectedItems[0].Tag!;
            if (Directory.Exists(full)) Navigate(full);
            else Process.Start(new ProcessStartInfo(full) { UseShellExecute = true });
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy hash", null, (_, _) => Copy(3));
        menu.Items.Add("Copy commit message", null, (_, _) => Copy(2));
        menu.Items.Add("Show in Windows Explorer", null, (_, _) =>
        {
            if (list.SelectedItems.Count > 0) Process.Start("explorer.exe", $"/select,\"{list.SelectedItems[0].Tag}\"");
        });
        list.ContextMenuStrip = menu;

        Controls.Add(list); Controls.Add(top);

        if (File.Exists(startPath)) Navigate(Path.GetDirectoryName(startPath)!, startPath);
        else Navigate(startPath);
    }

    void Copy(int col)
    {
        if (list.SelectedItems.Count > 0 && list.SelectedItems[0].SubItems[col].Text.Length > 0)
            Clipboard.SetText(list.SelectedItems[0].SubItems[col].Text);
    }

    async void Navigate(string dir, string? select = null)
    {
        if (!Directory.Exists(dir)) return;
        current = Path.GetFullPath(dir); pathBox.Text = current;
        int my = ++version;
        list.BeginUpdate(); list.Items.Clear();
        List<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(current).EnumerateFileSystemInfos()
                .Where(e => e.Name != ".git")
                .OrderByDescending(e => e is DirectoryInfo).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) { list.EndUpdate(); info.Text = ex.Message; return; }
        var items = new Dictionary<string, ListViewItem>();
        foreach (var e in entries)
        {
            var it = new ListViewItem(new[] { e.Name, "", "", "", "", "" }) { Tag = e.FullName };
            list.Items.Add(it); items[e.FullName] = it;
        }
        list.EndUpdate();
        if (select != null && items.TryGetValue(select, out var si)) { si.Selected = true; si.EnsureVisible(); }
        info.Text = "Reading git info...";

        var root = await Task.Run(() => GitService.RepoRoot(current));
        if (my != version) return;
        if (root == null) { info.Text = "Not inside a git repository."; return; }
        var status = await Task.Run(() => GitService.Status(root));
        var branch = await Task.Run(() => GitService.Branch(root));
        if (my != version) return;
        info.Text = $"Repo: {root}   Branch: {branch}   Changed files: {status.Count(k => k.Value != "Ignored")}";

        foreach (var e in entries)
        {
            var rel = Path.GetRelativePath(root, e.FullName).Replace('\\', '/');
            var s = GitService.StatusFor(status, rel, e is DirectoryInfo);
            var it = items[e.FullName];
            it.SubItems[1].Text = s;
            it.ForeColor = s.StartsWith("Modified") || s.StartsWith("Staged +") || s.StartsWith("Changed") ? Color.DarkOrange
                : s.StartsWith("Staged") || s.StartsWith("Added") ? Color.Green
                : s == "Ignored" ? Color.Gray : SystemColors.WindowText;
        }

        // Per-item commit info, in parallel, filled progressively.
        var sem = new SemaphoreSlim(8);
        await Task.WhenAll(entries.Select(async e =>
        {
            await sem.WaitAsync();
            try
            {
                var rel = Path.GetRelativePath(root, e.FullName).Replace('\\', '/');
                var c = await Task.Run(() => GitService.LastCommit(root, rel));
                if (my != version) return;
                var it = items[e.FullName];
                if (c != null) { it.SubItems[2].Text = c.Message; it.SubItems[3].Text = c.Hash; it.SubItems[4].Text = c.Author; it.SubItems[5].Text = c.Date; }
                else if (it.SubItems[1].Text.StartsWith("Added")) it.SubItems[2].Text = "(not committed yet)";
            }
            finally { sem.Release(); }
        }));
    }
}
