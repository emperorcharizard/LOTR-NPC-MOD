param([string[]]$cmds, [int]$gapMs = 400)
# Types each chat command into the game (T, paste, Enter). Every key checks the game is in front first and stops the
# script if it can't be brought back: keys once landed in the Claude window, where Enter sent them as messages.
Add-Type -Namespace WC3 -Name K -MemberDefinition @'
[DllImport("user32.dll")] public static extern void keybd_event(byte k, byte s, uint f, System.UIntPtr e);
[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(System.IntPtr h);
'@
$game = (Get-Process Vintagestory -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1).MainWindowHandle
function Front {
  for ($i = 0; $i -lt 3; $i++) {
    if ([WC3.K]::GetForegroundWindow() -eq $game) { return }
    [WC3.K]::keybd_event(0x12,0,0,[UIntPtr]::Zero); [WC3.K]::keybd_event(0x12,0,2,[UIntPtr]::Zero)
    [WC3.K]::SetForegroundWindow($game) | Out-Null; Start-Sleep -Milliseconds 250
  }
  throw "game window is not in front; stopped"
}
function Down([byte]$k) { Front; [WC3.K]::keybd_event($k,0,0,[UIntPtr]::Zero) }
function Up([byte]$k) { [WC3.K]::keybd_event($k,0,2,[UIntPtr]::Zero) }
function Tap([byte]$k) { Down $k; Start-Sleep -Milliseconds 30; Up $k; Start-Sleep -Milliseconds 30 }
foreach ($c in $cmds) {
  if ($c -match '^key:(\w+)$') { Tap ([byte][Convert]::ToInt32($Matches[1], 16)); Start-Sleep -Milliseconds $gapMs; continue }
  if ($c -match '^wait:(\d+)$') { Start-Sleep -Milliseconds ([int]$Matches[1]); continue }
  Set-Clipboard -Value $c
  Tap 0x54; Start-Sleep -Milliseconds 150
  Down 0x11; Tap 0x56; Up 0x11
  Start-Sleep -Milliseconds 80; Tap 0x0D; Start-Sleep -Milliseconds $gapMs
}
"done"
