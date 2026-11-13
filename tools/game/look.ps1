param([int]$dx = 0, [int]$dy = 0)
# Turns the game camera with raw relative mouse motion, in 10-count steps so no single jump is dropped.
Add-Type -Namespace WL -Name K -MemberDefinition '[DllImport("user32.dll")] public static extern void mouse_event(uint f, int x, int y, uint d, System.UIntPtr e);'
$steps = [Math]::Max(1, [Math]::Ceiling([Math]::Max([Math]::Abs($dx), [Math]::Abs($dy)) / 10))
$sx = 0; $sy = 0
for ($i = 1; $i -le $steps; $i++) {
  $tx = [int][Math]::Round($dx * $i / $steps); $ty = [int][Math]::Round($dy * $i / $steps)
  [WL.K]::mouse_event(0x0001, $tx - $sx, $ty - $sy, 0, [UIntPtr]::Zero)
  $sx = $tx; $sy = $ty
  Start-Sleep -Milliseconds 15
}
"moved $dx $dy"
