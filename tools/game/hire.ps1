param([byte]$slot = 0x34)
# Spawns a warrior from the egg in hotbar slot 4 at the crosshair, switches to empty slot 6 and right-clicks it to talk.
Add-Type -Namespace WH -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, System.UIntPtr e);
'@
function Tap([byte]$k) { [WH.K]::keybd_event($k,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 30; [WH.K]::keybd_event($k,0,2,[UIntPtr]::Zero) }
function RC { [WH.K]::mouse_event(0x0008,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 100; [WH.K]::mouse_event(0x0010,0,0,0,[UIntPtr]::Zero) }
Tap $slot; Start-Sleep -Milliseconds 150
RC; Start-Sleep -Milliseconds 100
Tap 0x36; Start-Sleep -Milliseconds 150
RC
"done"

