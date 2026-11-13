param([double]$x, [double]$y)
# Held left-click at screenshot-frame coords (1456x819 frame on a 1920x1080 screen); the game ignores instant clicks.
Add-Type -Namespace WC -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, System.UIntPtr e);
'@
$s = 1920 / 1456
[WC.K]::SetCursorPos([int]($x * $s), [int]($y * $s)) | Out-Null
Start-Sleep -Milliseconds 80
[WC.K]::mouse_event(0x0002,0,0,0,[UIntPtr]::Zero); Start-Sleep -Milliseconds 100; [WC.K]::mouse_event(0x0004,0,0,0,[UIntPtr]::Zero)
"clicked"
