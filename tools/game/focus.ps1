# Brings the Vintage Story window to the front without moving the mouse (a click would turn the camera).
Add-Type -Namespace WF -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
'@
$p = Get-Process Vintagestory -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
# A bare Alt tap lets a background process take the foreground.
[WF.K]::keybd_event(0x12,0,0,[UIntPtr]::Zero); [WF.K]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
[WF.K]::SetForegroundWindow($p.MainWindowHandle)
