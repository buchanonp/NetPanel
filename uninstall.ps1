# Removes NetPanel for the current user.
param([switch]$KeepSettings)
Get-Process NetPanel -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'NetPanel' -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\NetPanel') -Recurse -Force -ErrorAction SilentlyContinue
if (-not $KeepSettings) { Remove-Item (Join-Path $env:LOCALAPPDATA 'NetPanel') -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "NetPanel removed. DNS settings you applied are not reverted; use the DHCP button first if you want automatic DNS back."
