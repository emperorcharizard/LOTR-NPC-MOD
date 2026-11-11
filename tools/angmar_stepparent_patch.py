"""Writes tools/patches/angmar-stepparent.json (0.0.114): JSON patches that drop the empty "stepParentName" from the
parts of To Rule Them All: Angmar's armor shapes. The game looks for a parent named "" for each such part, logs
"Step parented shape ... requires step parent element with name , ... Will not be visible", and leaves the part where it
is (so it shows anyway): about 900 warnings a session with our NPCs in Angmar armor (client log 2026-11-11).
The patches apply only when Angmar is installed.

    python tools/angmar_stepparent_patch.py <angmar zip>    e.g. %APPDATA%/VintagestoryData/Mods/angmar0.2.0.zip
"""
import json
import os
import re
import sys
import zipfile

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "patches", "angmar-stepparent.json")


def lenient(text):
    text = re.sub(r'(?m)(?<!:)//.*$', "", text)
    return json.loads(re.sub(r",(\s*[}\]])", r"\1", text))


def empty_parents(elements, path):
    for i, e in enumerate(elements or []):
        here = f"{path}/{i}"
        if e.get("stepParentName") == "":
            yield here + "/stepParentName"
        yield from empty_parents(e.get("children"), here + "/children")


def main():
    patches = []
    with zipfile.ZipFile(sys.argv[1]) as z:
        for name in sorted(z.namelist()):
            m = re.match(r"assets/([^/]+)/shapes/(.+\.json)$", name)
            if not m:
                continue
            shape = lenient(z.read(name).decode("utf-8-sig"))
            for p in empty_parents(shape.get("elements"), "/elements"):
                patches.append({"op": "remove", "path": p, "file": f"{m.group(1)}:shapes/{m.group(2)}",
                                "dependsOn": [{"modid": "lotrangmar"}]})
    assert patches, "no empty step parent names found"
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("[\n" + ",\n".join(json.dumps(p, separators=(",", ":")) for p in patches) + "\n]\n")
    print(len(patches), "patches in", len({p["file"] for p in patches}), "shapes ->", OUT)


if __name__ == "__main__":
    main()
