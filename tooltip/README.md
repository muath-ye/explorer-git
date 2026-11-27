# tooltip — git info in the Explorer hover tooltip

Hover over a file or folder inside a git repo in Windows Explorer and the tooltip shows:

```
Git status: Committed (clean)
Commit: 1ddc8132
Message: Update URLs in templates and documentation…
Author: muath-ye
Date: 2026-11-13 13:22
```

Status values: Modified, Staged, Staged + Modified, Added (staged), Added (untracked), Committed (clean), Ignored. Folders show `Changed (n items)`.

It is an Explorer *infotip* shell extension (`IQueryInfo` + `IPersistFile`) written in plain C# and compiled with the `csc.exe` that ships with Windows. No third-party software.

## Install (no admin needed)

Registers under the current user (`HKCU`) only:

```powershell
cd C:\GroshIvyWallet\explorer-git\tooltip
.\install-tooltip.ps1 -RestartExplorer
```

Uninstall:

```powershell
.\install-tooltip.ps1 -Uninstall -RestartExplorer
```

`-RestartExplorer` restarts Explorer, closing open Explorer windows.

## Files

- `GitInfoTip.cs` — the COM infotip handler. Runs `git` with a 2.5 s timeout, results cached for 3 s.
- `install-tooltip.ps1` — compiles the DLL into `%LOCALAPPDATA%\ExplorerGitTip` and registers it for all files (`*`) and folders (`Directory`).

## Limits

- It is a tooltip, not a column. For columns see [`../explorer-columns`](../explorer-columns).
- File types that have their own tooltip handler (some images, Office files, executables) may ignore this one.
- Items outside a git repo keep the normal Windows tooltip.
- `git` must be on `PATH`.
