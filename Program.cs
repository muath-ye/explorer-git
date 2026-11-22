using ExplorerGit;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var path = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
        Application.Run(new MainForm(path));
    }
}
