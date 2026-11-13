param([int]$rounds = 20, [int]$gapMs = 6000)
# Keeps the player hovering over the middle of all test NPCs so Synergy's 48-block activation range never freezes any.
$s = $PSScriptRoot
$log = "$env:APPDATA\VintagestoryData\Logs\client-chat.log"
for ($i = 0; $i -lt $rounds; $i++) {
  & "$s\chat.ps1" -cmds @("/entity find e[type=lotrnpcs:test-*,range=200,alive=true]", "wait:700") | Out-Null
  $t = (Get-Content $log -Raw)
  $k = $t.LastIndexOf("===Found entities===")
  if ($k -lt 0) { continue }
  $pts = [regex]::Matches($t.Substring($k), " at (-?\d+), -?\d+, (-?\d+)")
  if ($pts.Count -eq 0) { "none left"; break }
  $xs = $pts | ForEach-Object { [int]$_.Groups[1].Value }; $zs = $pts | ForEach-Object { [int]$_.Groups[2].Value }
  $mx = [int](($xs | Measure-Object -Average).Average); $mz = [int](($zs | Measure-Object -Average).Average)
  $far = 0; foreach ($j in 0..($xs.Count - 1)) { $d = [math]::Sqrt(($xs[$j] - $mx) * ($xs[$j] - $mx) + ($zs[$j] - $mz) * ($zs[$j] - $mz)); if ($d -gt $far) { $far = $d } }
  & "$s\chat.ps1" -cmds @("/tp $mx 20 $mz") | Out-Null
  "{0:HH:mm:ss} n={1} centre {2},{3} farthest {4:0}" -f (Get-Date), $pts.Count, $mx, $mz, $far
  Start-Sleep -Milliseconds $gapMs
}
