# Restarts Vintage Story into the test world with the latest lotrnpcs build, never pressing Escape (Escape ends Claude's turn):
# /stop saves and shuts the world, then the disconnect screen's button, the main menu's Quit, copy the zip, launch, open the world.
param([string[]]$before = @())
$S = $PSScriptRoot; $o = $PSScriptRoot
$repo = Split-Path (Split-Path $PSScriptRoot)
& "$S\chat.ps1" -cmds ($before + @("/stop", "wait:9000")) | Out-Null
& "$o\focus.ps1" | Out-Null; Start-Sleep -Milliseconds 300; & "$o\click.ps1" 727 578 | Out-Null; Start-Sleep -Seconds 2
& "$o\click.ps1" 110 523 | Out-Null; Start-Sleep -Seconds 8
if (Get-Process Vintagestory -ErrorAction SilentlyContinue) { "still running"; return }
$zip = Get-ChildItem "$repo\releases" -Filter "lotrnpcs_*-test.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
Get-ChildItem "C:\Users\Mason\AppData\Roaming\VintagestoryData\Mods" -Filter "lotrnpcs_*.zip" | Where-Object Name -ne $zip.Name | Remove-Item
Copy-Item $zip.FullName "C:\Users\Mason\AppData\Roaming\VintagestoryData\Mods\" -Force
Start-Process "C:\Users\Mason\AppData\Roaming\Vintagestory\Vintagestory.exe"; Start-Sleep -Seconds 25
& "$o\focus.ps1" | Out-Null; Start-Sleep -Milliseconds 400; & "$o\click.ps1" 110 280 | Out-Null; Start-Sleep -Seconds 2; & "$o\click.ps1" 700 116 | Out-Null; Start-Sleep -Seconds 60
"relaunched with " + $zip.Name
