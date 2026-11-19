"""One-command build of the lotrnpcs test zip (0.0.112): DLL, generated assets, the orc models made from LRC, both offline test
suites, then releases/lotrnpcs_<version>-test.zip. Runs on Windows and Linux (GitHub Actions).

    python tools/build.py              build, test, zip
    python tools/build.py --no-zip     build and test only (CI)
    python tools/build.py --publish    also upload the zip as GitHub release v<version> (needs `gh auth login` once)
    python tools/build.py --release    an official release: releases/lotrnpcs_<version>.zip (no -test), with modicon.png and
                                       LICENSE; with --publish, the release notes come from docs/release-notes.md

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


def check_lrc_refs(assets):
    """1.0.4: LOTR Races and Classes is a required mod, so its shapes and textures are named, not copied. Every lrc:
    shape or texture our files name must be in LRC."""
    import re
    lrc = os.path.join(LRC, "assets", "lrc")
    count = 0
    for folder, _, files in os.walk(assets):
        for name in files:
            if name.endswith(".json"):
                for ref in set(re.findall(r'"lrc:([a-z0-9_/.-]+)"', open(os.path.join(folder, name), encoding="utf-8-sig").read())):
                    path = ref.removeprefix("shapes/").removesuffix(".json")
                    if "{code}" in ref or "/" not in ref:
                        continue
                    count += 1
                    if not any(os.path.isfile(os.path.join(lrc, kind, path + ext)) for kind, ext in (("shapes", ".json"), ("textures", ".png"))):
                        sys.exit(f"{name} names lrc:{ref}, which isn't in LRC")
    print(f"lrc: {count} references checked")


def packed(path):
    """JSON goes into the zip as generated (indented), so people reading or patching the mod's files can find their
    way; zip compression makes the indentation nearly free."""
    return open(path, "rb").read()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--no-zip", action="store_true")
    ap.add_argument("--publish", action="store_true")
    ap.add_argument("--release", action="store_true")
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
    # The orc models and the patch adding our weapon animations to LRC's bodies; checks tools/data/races.json.
    run([sys.executable, os.path.join(ROOT, "tools", "lrc_models.py"), os.path.join(LRC, "assets", "lrc"), os.path.join(GEN, "assets", "lotrnpcs")])
    check_lrc_refs(os.path.join(GEN, "assets", "lotrnpcs"))
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
    out = os.path.join(ROOT, "releases", f"lotrnpcs_{version}.zip" if opts.release else f"lotrnpcs_{version}-test.zip")
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(os.path.join(GEN, "modinfo.json"), "modinfo.json")
        z.write(os.path.join(ROOT, "src", "out", "lotrnpcs.dll"), "lotrnpcs.dll")
        z.write(os.path.join(ROOT, "tools", "CREDITS.txt"), "CREDITS.txt")
        # Release extras (1.0.0): the mod icon the game shows in its mod list, and the license.
        for extra, name in ((os.path.join(ROOT, "tools", "modicon.png"), "modicon.png"), (os.path.join(ROOT, "LICENSE"), "LICENSE")):
            if os.path.isfile(extra):
                z.write(extra, name)
        for folder, _, files in os.walk(os.path.join(GEN, "assets")):
            for name in sorted(files):
                path = os.path.join(folder, name)
                # Forward slashes: the game can't read zip entries with backslashes.
                z.writestr(os.path.relpath(path, GEN).replace(os.sep, "/"), packed(path), zipfile.ZIP_DEFLATED, 9)
    print("wrote", out)
    # 1.0.5: the example faction pack (tools/examplepack), a content mod adding the Lindon faction.
    pack = os.path.join(ROOT, "tools", "examplepack")
    pack_version = json.load(open(os.path.join(pack, "modinfo.json"), encoding="utf-8"))["version"]
    pack_out = os.path.join(ROOT, "releases", f"lindonnpcs_{pack_version}.zip")
    with zipfile.ZipFile(pack_out, "w", zipfile.ZIP_DEFLATED) as z:
        for folder, _, files in os.walk(pack):
            for name in sorted(files):
                path = os.path.join(folder, name)
                z.write(path, os.path.relpath(path, pack).replace(os.sep, "/"))
    print("wrote", pack_out)

    if opts.publish:
        gh = shutil.which("gh") or r"C:\Program Files\GitHub CLI\gh.exe"
        if opts.release:
            run([gh, "release", "create", f"v{version}", out, "--title", f"LOTR NPCs {version}",
                 "--notes-file", os.path.join(ROOT, "docs", "release-notes.md")])
        else:
            run([gh, "release", "create", f"v{version}", out, "--title", f"lotrnpcs {version} (test)",
                 "--notes", f"Test build {version}. What to check: docs/test-checklist.md."])


if __name__ == "__main__":
    main()
