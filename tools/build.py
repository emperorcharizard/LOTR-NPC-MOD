"""One-command build of the lotrnpcs test zip (0.0.112): DLL, generated assets, bundled LRC races, both offline test
suites, then releases/lotrnpcs_<version>-test.zip. Runs on Windows and Linux (GitHub Actions).

    python tools/build.py              build, test, zip
    python tools/build.py --no-zip     build and test only (CI)
    python tools/build.py --publish    also upload the zip as GitHub release v<version> (needs `gh auth login` once)

Run with plain `python`, not `python -I`: the generator needs Pillow, which may live in the user's site-packages.
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
BUILD = os.path.join(ROOT, "build")          # scratch, ignored by git
GEN = os.path.join(BUILD, "gen")
LRC = os.path.join(BUILD, "lrc")
LRC_ZIP = os.path.join(ROOT, "build-inputs", "lrc-0.4.1.zip")


def dotnet():
    found = shutil.which("dotnet")
    if found:
        return found
    win = r"C:\Program Files\dotnet\dotnet.exe"
    if os.path.exists(win):
        return win
    sys.exit("dotnet not found: install the .NET 10 SDK")


def run(args, cwd=ROOT):
    print("+", " ".join(str(a) for a in args), flush=True)
    result = subprocess.run(args, cwd=cwd)
    if result.returncode != 0:
        sys.exit(f"failed ({result.returncode}): {args[0]}")


SKINS = os.path.join("textures", "entity", "humanoid", "skinparts")


def dedupe_skins(assets):
    """0.0.134 (owner, 2026-11-14: "debloat the size of the modfile"): LRC ships the same skin picture under several
    races (dwarfskin1 = elfskin4 = hobbitskin8 = humanskin4, ...). Each picture is kept once, under the first of its
    paths in sorted order, and every JSON reference to the other paths points there instead (about 350 KB)."""
    import hashlib
    root = os.path.join(assets, SKINS)
    byhash = {}
    for folder, _, files in os.walk(root):
        for name in files:
            if name.endswith(".png"):
                path = os.path.join(folder, name)
                byhash.setdefault(hashlib.sha256(open(path, "rb").read()).hexdigest(), []).append(path)
    code = lambda path: "lotrnpcs:" + os.path.relpath(path, os.path.join(assets, "textures"))[:-4].replace(os.sep, "/")
    moved = {}
    for paths in byhash.values():
        keep, *drop = sorted(paths, key=code)
        for path in drop:
            moved[code(path) + '"'] = code(keep) + '"'
            os.remove(path)
    for folder, _, files in os.walk(assets):
        for name in files:
            if name.endswith(".json"):
                path = os.path.join(folder, name)
                text = open(path, encoding="utf-8-sig").read()
                new = text
                for old, keep in moved.items():
                    new = new.replace('"' + old, '"' + keep)
                if new != text:
                    open(path, "w", encoding="utf-8").write(new)
    # Every skin a JSON file names must still exist.
    import re
    for folder, _, files in os.walk(assets):
        for name in files:
            if name.endswith(".json"):
                for ref in re.findall(r'"lotrnpcs:(entity/humanoid/skinparts/[^"]+)"', open(os.path.join(folder, name), encoding="utf-8-sig").read()):
                    if not os.path.isfile(os.path.join(assets, "textures", ref + ".png")):
                        sys.exit(f"missing skin {ref} in {name}")
    print(f"skins: {len(moved)} duplicate pictures dropped")


def packed(path):
    """0.0.134: JSON goes into the zip without indentation (about half the unpacked size). Files the strict parser
    can't read (// credit comments, trailing commas) go in as they are."""
    data = open(path, "rb").read()
    if not path.endswith(".json"):
        return data
    try:
        return json.dumps(json.loads(data.decode("utf-8-sig")), separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    except ValueError:
        return data


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-zip", action="store_true")
    ap.add_argument("--publish", action="store_true")
    opts = ap.parse_args()
    dn = dotnet()

    run([dn, "build", "-c", "Release", "-o", "out"], cwd=os.path.join(ROOT, "src"))

    shutil.rmtree(GEN, ignore_errors=True)
    run([sys.executable, os.path.join(ROOT, "tools", "gen_testnpcs.py"), GEN])
    # JSON patches kept as files (0.0.114: tools/angmar_stepparent_patch.py writes the Angmar one).
    shutil.copytree(os.path.join(ROOT, "tools", "patches"), os.path.join(GEN, "assets", "lotrnpcs", "patches"))
    if not os.path.isdir(os.path.join(LRC, "assets", "lrc")):
        with zipfile.ZipFile(LRC_ZIP) as z:
            z.extractall(LRC)
    # bundle_lrc writes our own body shapes (with the poleaxe and greatsword animations).
    run([sys.executable, os.path.join(ROOT, "tools", "bundle_lrc.py"), os.path.join(LRC, "assets", "lrc"), os.path.join(GEN, "assets", "lotrnpcs")])
    dedupe_skins(os.path.join(GEN, "assets", "lotrnpcs"))
    entities = os.path.join(GEN, "assets", "lotrnpcs", "entities")

    fac = os.path.join(ROOT, "tools", "tests", "factions")
    run([dn, "build", "-c", "Release", "-o", os.path.join("bin", "run")], cwd=fac)
    for name in os.listdir(fac):
        if name.startswith("vanilla-") and name.endswith(".txt"):
            shutil.copy(os.path.join(fac, name), os.path.join(fac, "bin"))
    run([dn, os.path.join(fac, "bin", "run", "factest.dll"), entities], cwd=os.path.join(fac, "bin", "run"))

    zones = os.path.join(ROOT, "tools", "tests", "spawnzones")
    run([dn, "build", "-c", "Release", "-o", "out"], cwd=zones)
    refs = os.path.join(ROOT, "tools", "refs")
    for name in os.listdir(refs):
        if name.endswith(".dll"):
            shutil.copy(os.path.join(refs, name), os.path.join(zones, "out"))
    run([dn, os.path.join(zones, "out", "zonetest.dll"), entities], cwd=zones)

    if opts.no_zip:
        return
    version = json.load(open(os.path.join(GEN, "modinfo.json"), encoding="utf-8"))["version"]
    os.makedirs(os.path.join(ROOT, "releases"), exist_ok=True)
    out = os.path.join(ROOT, "releases", f"lotrnpcs_{version}-test.zip")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(os.path.join(GEN, "modinfo.json"), "modinfo.json")
        z.write(os.path.join(ROOT, "src", "out", "lotrnpcs.dll"), "lotrnpcs.dll")
        z.write(os.path.join(ROOT, "tools", "CREDITS.txt"), "CREDITS.txt")
        for folder, _, files in os.walk(os.path.join(GEN, "assets")):
            for name in sorted(files):
                path = os.path.join(folder, name)
                # Forward slashes: the game can't read zip entries with backslashes.
                z.writestr(os.path.relpath(path, GEN).replace(os.sep, "/"), packed(path), zipfile.ZIP_DEFLATED, 9)
    print("wrote", out)

    if opts.publish:
        gh = shutil.which("gh") or r"C:\Program Files\GitHub CLI\gh.exe"
        run([gh, "release", "create", f"v{version}", out, "--title", f"lotrnpcs {version} (test)",
             "--notes", f"Test build {version}. What to check: docs/test-checklist.md."])


if __name__ == "__main__":
    main()
