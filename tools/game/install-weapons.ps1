# Builds LOTR Weapons from a branch and installs it into the game's Mods folder, replacing older lotrweapons zips.
# Leaves your own checkout alone: the branch is built in a temporary git worktree, which is removed afterwards.
# Needs git and Python with Pillow (no .NET). Run from anywhere inside the repo:
#   powershell -ExecutionPolicy Bypass -File tools\game\install-weapons.ps1
#   ... -branch main          (another branch)
param([string]$branch = "claude/custom-weapons-mod-crafting-c99a2r")
$ErrorActionPreference = "Stop"
$repo = Split-Path (Split-Path $PSScriptRoot)
$mods = Join-Path $env:APPDATA "VintagestoryData\Mods"
$work = Join-Path $env:TEMP "lotrweapons-build"

git -C $repo fetch origin $branch
if (Test-Path $work) { git -C $repo worktree remove --force $work 2>$null; Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
git -C $repo worktree add --detach $work "origin/$branch"
try {
    # The worktree has no build-inputs zips that git ignores; the LRC zip is tracked, so the checks run as in CI.
    python (Join-Path $work "tools\build.py") --weapons-only
    if ($LASTEXITCODE -ne 0) { throw "build failed" }
    $zip = Get-ChildItem (Join-Path $work "releases") -Filter "lotrweapons_*-test.zip" | Sort-Object LastWriteTime | Select-Object -Last 1
    New-Item -ItemType Directory -Force $mods | Out-Null
    Get-ChildItem $mods -Filter "lotrweapons_*.zip" | Where-Object Name -ne $zip.Name | Remove-Item
    Copy-Item $zip.FullName $mods -Force
    "installed " + $zip.Name + " in " + $mods + " (restart the game to load it)"
} finally {
    git -C $repo worktree remove --force $work
}
