# Installs the Explorer hover-tooltip git extension for the CURRENT USER (HKCU only, no admin).
#   .\install-tooltip.ps1                  build + register
#   .\install-tooltip.ps1 -Uninstall       unregister
# Explorer must be restarted afterwards (the script offers -RestartExplorer).
param([switch]$Uninstall, [switch]$RestartExplorer)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$clsid = '{6F1B2C3A-8D4E-4A57-9B61-3C2E5D7A9F10}'
$iid   = '{00021500-0000-0000-C000-000000000046}'
$base  = 'HKCU:\Software\Classes'
$handlerKeys = @("$base\*\shellex\$iid", "$base\Directory\shellex\$iid")

foreach ($k in $handlerKeys) { if (Test-Path -LiteralPath $k) { Remove-Item -LiteralPath $k -Recurse -Force } }
if (Test-Path "$base\CLSID\$clsid") { Remove-Item "$base\CLSID\$clsid" -Recurse -Force }

if ($Uninstall) {
    Write-Host 'Unregistered. Restart Explorer (or sign out) to unload it.'
}
else {
    $dest = Join-Path $env:LOCALAPPDATA 'ExplorerGitTip'
    New-Item -ItemType Directory -Force $dest | Out-Null
    # Explorer locks a loaded DLL, so use a fresh file name per install.
    $dll = Join-Path $dest ("ExplorerGitTip-" + (Get-Date -Format 'yyyyMMddHHmmss') + ".dll")
    $csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    & $csc /nologo /target:library /platform:anycpu "/out:$dll" "$here\GitInfoTip.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
    Get-ChildItem $dest -Filter 'ExplorerGitTip-*.dll' | Where-Object { $_.FullName -ne $dll } | ForEach-Object { try { Remove-Item $_.FullName -Force } catch {} }

    $asm = [System.Reflection.AssemblyName]::GetAssemblyName($dll).FullName
    $in = "$base\CLSID\$clsid\InprocServer32"
    New-Item -Path $in -Force | Out-Null
    Set-ItemProperty -LiteralPath "$base\CLSID\$clsid" -Name '(default)' -Value 'Explorer Git Info Tip'
    Set-ItemProperty -LiteralPath $in -Name '(default)' -Value 'mscoree.dll'
    Set-ItemProperty -LiteralPath $in -Name 'ThreadingModel' -Value 'Both'
    Set-ItemProperty -LiteralPath $in -Name 'Class' -Value 'ExplorerGitTip.GitInfoTip'
    Set-ItemProperty -LiteralPath $in -Name 'Assembly' -Value $asm
    Set-ItemProperty -LiteralPath $in -Name 'RuntimeVersion' -Value 'v4.0.30319'
    Set-ItemProperty -LiteralPath $in -Name 'CodeBase' -Value ([Uri]$dll).AbsoluteUri

    foreach ($k in $handlerKeys) {
        New-Item -Path $k -Force | Out-Null
        Set-ItemProperty -LiteralPath $k -Name '(default)' -Value $clsid
    }
    Write-Host "Registered. Restart Explorer, then hover over a file inside a git repo."
}
if ($RestartExplorer) { Stop-Process -Name explorer -Force; Start-Sleep 1; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer.exe } }
