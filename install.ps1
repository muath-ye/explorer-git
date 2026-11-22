# Builds ExplorerGit and adds "Git Info" to the Explorer right-click menu (current user only, no admin needed).
# Usage:  .\install.ps1            install
#         .\install.ps1 -Uninstall remove
param([switch]$Uninstall)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $env:LOCALAPPDATA 'ExplorerGit'
$keys = @('HKCU:\Software\Classes\*\shell\ExplorerGit',
          'HKCU:\Software\Classes\Directory\shell\ExplorerGit',
          'HKCU:\Software\Classes\Directory\Background\shell\ExplorerGit')
foreach ($k in $keys) { if (Test-Path -LiteralPath $k) { Remove-Item -LiteralPath $k -Recurse -Force } }
if ($Uninstall) { if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }; Write-Host 'Uninstalled.'; return }

dotnet publish "$here\ExplorerGit.csproj" -c Release -r win-x64 --self-contained false -o $dest | Out-Null
$exe = Join-Path $dest 'ExplorerGit.exe'
$args = @('"%1"', '"%1"', '"%V"')
for ($i = 0; $i -lt $keys.Count; $i++) {
    $k = $keys[$i]
    New-Item -Path "$k\command" -Force | Out-Null
    Set-ItemProperty -LiteralPath $k -Name '(default)' -Value 'Git Info (commit, author, status)'
    Set-ItemProperty -LiteralPath $k -Name 'Icon' -Value $exe
    Set-ItemProperty -LiteralPath "$k\command" -Name '(default)' -Value "`"$exe`" $($args[$i])"
}
Write-Host "Installed to $dest. Right-click a file/folder (Win11: 'Show more options') -> Git Info."
