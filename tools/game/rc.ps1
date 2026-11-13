param([string]$keys = "")
# Held right-click at the crosshair, then optional menu keys (e.g. "6" or "61"), 600 ms apart.
Add-Type -Namespace WR -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, System.UIntPtr e);
'@
[WR.K]::mouse_event(0x0008,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 100; [WR.K]::mouse_event(0x0010,0,0,0,[UIntPtr]::Zero)
foreach ($c in $keys.ToCharArray()) {
  Start-Sleep -Milliseconds 700
  $vk = [byte][char]::ToUpper($c)
  [WR.K]::keybd_event($vk,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 30; [WR.K]::keybd_event($vk,0,2,[UIntPtr]::Zero)
}
"done"
