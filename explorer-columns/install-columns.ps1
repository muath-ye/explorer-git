# Adds real Explorer "Details" columns: Git Status, Git Commit, Git Message, Git Author, Git Date.
# MUST be run from an ELEVATED (Administrator) PowerShell: property handlers/schemas are machine-wide.
#
#   .\install-columns.ps1                       install for ALL registered file extensions + folders
#   .\install-columns.ps1 -Extensions .kt,.xml  install for specific extensions only (+ folders)
#   .\install-columns.ps1 -Uninstall            remove everything
#
# Safety: an extension that ALREADY has a property handler (images, Office, PDF, media...) is skipped,
# so no existing Windows behaviour is replaced. System-critical types are always excluded.
param(
    [string[]]$Extensions,
    [switch]$NoFolders,
    [switch]$Uninstall,
    [switch]$RestartExplorer
)
$Exclude = @('.exe','.dll','.sys','.lnk','.url','.msi','.scr','.cpl','.ocx','.drv','.com','.library-ms','.search-ms','.zip','.cab')
$ErrorActionPreference = 'Stop'
$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw 'Run this script from an elevated (Administrator) PowerShell.' }

$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$data   = Join-Path $env:ProgramData 'ExplorerGitColumns'
$clsid  = '{A3D5E7F1-2B4C-4D6E-8F90-1A2B3C4D5E6F}'
$phRoot = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PropertySystem\PropertyHandlers'
$clsKey = "HKLM:\SOFTWARE\Classes\CLSID\$clsid"
$stateF = Join-Path $data 'installed-extensions.txt'

Add-Type -Namespace Native -Name PropSys -MemberDefinition @'
[DllImport("propsys.dll", CharSet=CharSet.Unicode)] public static extern int PSRegisterPropertySchema(string path);
[DllImport("propsys.dll", CharSet=CharSet.Unicode)] public static extern int PSUnregisterPropertySchema(string path);
[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);
'@

function Remove-All {
    if (Test-Path $stateF) {
        foreach ($ext in Get-Content $stateF) {
            $k = "$phRoot\$ext"
            if ((Test-Path -LiteralPath $k) -and ((Get-ItemProperty -LiteralPath $k).'(default)' -eq $clsid)) { Remove-Item -LiteralPath $k -Recurse -Force }
        }
    }
    $pd = Get-ChildItem $data -Filter 'ExplorerGit-*.propdesc' -ErrorAction SilentlyContinue
    foreach ($f in $pd) { [void][Native.PropSys]::PSUnregisterPropertySchema($f.FullName) }
    if (Test-Path $clsKey) { Remove-Item $clsKey -Recurse -Force }
}

Remove-All
if ($Uninstall) {
    [Native.PropSys]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
    Write-Host 'Uninstalled. Restart Explorer / sign out to fully unload (DLL may stay locked until then).'
}
else {
    New-Item -ItemType Directory -Force $data | Out-Null
    $stamp = Get-Date -Format 'yyyyMMddHHmmss'
    $dll   = Join-Path $data "ExplorerGitColumns-$stamp.dll"
    $csc   = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    & $csc /nologo /target:library /platform:x64 "/out:$dll" "$here\GitColumns.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
    Get-ChildItem $data -Filter 'ExplorerGitColumns-*.dll' | Where-Object { $_.FullName -ne $dll } | ForEach-Object { try { Remove-Item $_.FullName -Force } catch {} }

    # COM registration (64-bit, machine-wide)
    $asm = [System.Reflection.AssemblyName]::GetAssemblyName($dll).FullName
    $in  = "$clsKey\InprocServer32"
    New-Item -Path $in -Force | Out-Null
    Set-ItemProperty -LiteralPath $clsKey -Name '(default)' -Value 'Explorer Git Property Handler'
    Set-ItemProperty -LiteralPath $in -Name '(default)' -Value 'mscoree.dll'
    Set-ItemProperty -LiteralPath $in -Name 'ThreadingModel' -Value 'Both'
    Set-ItemProperty -LiteralPath $in -Name 'Class' -Value 'ExplorerGitColumns.GitPropertyHandler'
    Set-ItemProperty -LiteralPath $in -Name 'Assembly' -Value $asm
    Set-ItemProperty -LiteralPath $in -Name 'RuntimeVersion' -Value 'v4.0.30319'
    Set-ItemProperty -LiteralPath $in -Name 'CodeBase' -Value ([Uri]$dll).AbsoluteUri
    New-Item -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" -Force | Out-Null
    Set-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved" -Name $clsid -Value 'Explorer Git Property Handler'

    # Property schema (defines the columns)
    $pd = Join-Path $data "ExplorerGit-$stamp.propdesc"
    Copy-Item "$here\ExplorerGit.propdesc" $pd -Force
    $hr = [Native.PropSys]::PSRegisterPropertySchema($pd)
    if ($hr -ne 0) { throw ("PSRegisterPropertySchema failed: 0x{0:X8}" -f $hr) }

    # Handlers per extension (skip extensions that already have one)
    if (-not $Extensions -or $Extensions.Count -eq 0) {
        $Extensions = [Microsoft.Win32.Registry]::ClassesRoot.GetSubKeyNames() | Where-Object { $_.StartsWith('.') }
    }
    $targets = @()
    foreach ($e in $Extensions) { if (-not $e.StartsWith('.')) { $e = ".$e" }; $targets += $e }
    if (-not $NoFolders) { $targets += @('Directory', 'Folder') }   # folders (best effort)

    $done = @(); $skipped = 0
    foreach ($ext in ($targets | Select-Object -Unique)) {
        if ($Exclude -contains $ext.ToLowerInvariant()) { $skipped++; continue }
        $k = "$phRoot\$ext"
        if (Test-Path -LiteralPath $k) { $skipped++; continue }   # already has a handler: never replace it
        New-Item -Path $k -Force | Out-Null
        Set-ItemProperty -LiteralPath $k -Name '(default)' -Value $clsid
        $done += $ext
    }
    $done | Set-Content $stateF
    [Native.PropSys]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
    Write-Host "Registered for $($done.Count) types (skipped $skipped that already have a handler or are excluded)."
    Write-Host "Restart Explorer, then: Details view -> right-click column header -> More... -> tick the 'Git ...' columns."
    Write-Host "To make them the default for every folder: Explorer > ... > Options > View > 'Apply to Folders'."
}
if ($RestartExplorer) { Stop-Process -Name explorer -Force; Start-Sleep 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe } }
