<img width="1507" height="333" alt="image" src="https://github.com/user-attachments/assets/77e7a778-cc7f-458c-ac5b-53f54244fef9" />

# explorer-git

Show git information — **commit message, commit hash, author and file status** (modified / added / staged / committed) — for files and folders on Windows.

Three independent tools, from most to least integrated with Explorer:

| Folder | What it gives you | Admin? |
|---|---|---|
| [`explorer-columns/`](explorer-columns) | Real **columns** in the Explorer Details view (Git Status, Commit, Message, Author, Date) | Yes |
| [`tooltip/`](tooltip) | Git details in the Explorer **hover tooltip** | No |
| *(this folder)* `ExplorerGit` app | A standalone **Git Info window** with all columns, opened from a right-click menu entry | No |

All use only what ships with Windows plus .NET and `git`; no third-party libraries.

## Why not just one?

Windows 11 Explorer has no API for arbitrary custom columns or status text. Columns can only come from the Windows *property system* (property handlers), which has limits (see its README). The tooltip works for most files without admin rights. The standalone window works everywhere and shows everything at once.

## Standalone window (`ExplorerGit`)

Files: `ExplorerGit.csproj`, `Program.cs`, `MainForm.cs`, `GitService.cs`, `install.ps1`.

- Browse any folder; columns: Name, Status, Commit message, Hash, Author, Date. Colour-coded status, repo/branch header, copy hash/message from the right-click menu.
- Requires the .NET 9 desktop runtime and the .NET SDK to build.

Install (no admin; adds **Git Info** to the Explorer right-click menu for files, folders and folder backgrounds — on Windows 11 under *Show more options*):

```powershell
cd C:\GroshIvyWallet\explorer-git
.\install.ps1            # build to %LOCALAPPDATA%\ExplorerGit and register the menu entry
.\install.ps1 -Uninstall
```

Run without installing: `dotnet run -- <path>`.

## Requirements

- Windows 10/11 (64-bit), `git` on `PATH`
- .NET Framework 4 (built in) for `tooltip` and `explorer-columns`
- .NET 9 SDK/runtime for the standalone window

## Layout

```
explorer-git/
├─ README.md
├─ ExplorerGit.csproj, Program.cs, MainForm.cs, GitService.cs, install.ps1   standalone window
├─ tooltip/            hover tooltip (HKCU, no admin)
└─ explorer-columns/   real Explorer columns (HKLM, admin)
```
