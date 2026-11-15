param([string]$mods = "ctrl+shift", [int]$ms = 150)
# Held right-click at the crosshair with modifier keys held (Ctrl+Shift = place on ground, Ctrl = tool rack), game window checked first.
Add-Type -Namespace WPR -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, System.UIntPtr e);
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
'@
$game = (Get-Process Vintagestory -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle
& "$PSScriptRoot\focus.ps1" | Out-Null; Start-Sleep -Milliseconds 250
if ([WPR.K]::GetForegroundWindow() -ne $game) { throw "game window is not in front" }
$vk = @{ ctrl = 0x11; shift = 0x10; alt = 0x12 }
$keys = @($mods -split "\+" | Where-Object { $_ } | ForEach-Object { [byte]$vk[$_] })
foreach ($k in $keys) { [WPR.K]::keybd_event($k,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 60 }
[WPR.K]::mouse_event(0x0008,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds $ms; [WPR.K]::mouse_event(0x0010,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 60
[array]::Reverse($keys); foreach ($k in $keys) { [WPR.K]::keybd_event($k,0,2,[UIntPtr]::Zero) }
"right-clicked with $mods"
