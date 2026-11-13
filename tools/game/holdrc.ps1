param([int]$ms = 3000)
# Holds the right mouse button for $ms milliseconds (harvesting a body with a knife), game window checked first.
Add-Type -Namespace WHR -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, System.UIntPtr e);
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
'@
$game = (Get-Process Vintagestory -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle
& "$PSScriptRoot\focus.ps1" | Out-Null; Start-Sleep -Milliseconds 250
if ([WHR.K]::GetForegroundWindow() -ne $game) { throw "game window is not in front" }
[WHR.K]::mouse_event(0x0008,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds $ms; [WHR.K]::mouse_event(0x0010,0,0,0,[UIntPtr]::Zero)
"held $ms ms"
