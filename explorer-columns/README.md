# explorer-columns — git info as real Windows Explorer columns

Adds five columns to the **Details** view of the normal Windows Explorer:

| Column | Example |
|---|---|
| Git Status | `Committed`, `Modified`, `Staged`, `Staged + Modified`, `Added (staged)`, `Added (untracked)`, `Ignored` |
| Git Commit | `1ddc8132` (hash of the last commit that touched the item) |
| Git Message | `feat: Update notification icon…` |
| Git Author | `muath-ye` |
| Git Date | `2026-11-21 22:47` |

No separate window and no third-party software: a small .NET Framework 4 property handler compiled with the `csc.exe` that ships with Windows.

## Install (needs Administrator)

Property handlers and schemas are machine-wide, so use an **elevated** PowerShell (Start → PowerShell → Run as administrator):

```powershell
cd C:\GroshIvyWallet\explorer-git\explorer-columns
Set-ExecutionPolicy -Scope Process Bypass     # only if scripts are blocked
.\install-columns.ps1 -RestartExplorer
```

`-RestartExplorer` restarts Explorer, closing open Explorer windows.

Options:

| Option | Meaning |
|---|---|
| *(none)* | Register for **all** registered file extensions **and** folders |
| `-Extensions .kt,.xml` | Only these extensions (folders still included) |
| `-NoFolders` | Do not register for folders |
| `-Uninstall` | Remove the handler, schema and registry keys |

## Show the columns

1. Open a folder inside a git repo and switch to **Details** view.
2. Right-click a column header → **More…** → tick **Git Status / Commit / Message / Author / Date**.
3. To make them the default in every folder: **… menu → Options → View → Apply to Folders**.

Windows stores default column layouts in protected system folder-type definitions, so step 3 (a one-time click) is the supported way to make them the default.

## How it works

- `GitColumns.cs` — COM property handler (`IInitializeWithFile` + `IPropertyStore`). For each item it runs `git status --porcelain` and `git log -1` and returns the values. Results are cached for 20 s; each git call has a 4 s timeout.
- `ExplorerGit.propdesc` — property schema defining the five columns (registered with `PSRegisterPropertySchema`).
- `install-columns.ps1` — compiles the DLL to `%ProgramData%\ExplorerGitColumns`, registers the COM class, the schema, and the handler for each extension.

## Safety and limits

- Extensions that **already have a property handler** (images, Office, PDF, audio/video…) are skipped, so Windows' built-in properties are never replaced. A few system types (`.exe`, `.dll`, `.lnk`, `.zip`…) are always excluded.
- Because of that, columns are blank for types Windows already handles itself.
- Files with **no extension** cannot be covered by this mechanism.
- **Folder rows are best effort**: Windows may not query property handlers for folders. If folder rows stay blank, only files are covered.
- Files outside a git repo show empty values.
- A managed (.NET) handler runs inside Explorer / Windows Search. If anything misbehaves, run `.\install-columns.ps1 -Uninstall -RestartExplorer`.
- 64-bit only. `git` must be on `PATH`.
