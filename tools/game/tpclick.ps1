param([string]$cmd, [int]$delayMs = 250)
# Types a chat command into the focused game, then holds a right-click so it lands before NPCs wander off.
Add-Type -Namespace W -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, System.UIntPtr e);
'@
function Tap([byte]$k) { [W.K]::keybd_event($k,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 30; [W.K]::keybd_event($k,0,2,[UIntPtr]::Zero); Start-Sleep -Milliseconds 30 }
Set-Clipboard -Value $cmd
Tap 0x54; Start-Sleep -Milliseconds 150
[W.K]::keybd_event(0x11,0,0,[UIntPtr]::Zero); Tap 0x56; [W.K]::keybd_event(0x11,0,2,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
Tap 0x0D
Start-Sleep -Milliseconds $delayMs
[W.K]::mouse_event(0x0008,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 120; [W.K]::mouse_event(0x0010,0,0,0,[UIntPtr]::Zero)
"done"
