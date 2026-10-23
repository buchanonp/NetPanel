# Installs NetPanel for the current user (no admin needed).
param([string]$Exe = (Join-Path $PSScriptRoot 'NetPanel.exe'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "NetPanel.exe not found next to install.ps1 (run build.cmd or download it from Releases)." }
$dest = Join-Path $env:LOCALAPPDATA 'Programs\NetPanel'
Get-Process NetPanel -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $Exe (Join-Path $dest 'NetPanel.exe') -Force
Unblock-File (Join-Path $dest 'NetPanel.exe') -ErrorAction SilentlyContinue
$target = Join-Path $dest 'NetPanel.exe'
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NetPanel' -Value "`"$target`""
Start-Process $target
Write-Host "NetPanel installed to $dest and set to start with Windows."
Write-Host "Tip: drag the tray icon out of the overflow area (or enable it in Settings > Personalization > Taskbar) to keep it visible."
