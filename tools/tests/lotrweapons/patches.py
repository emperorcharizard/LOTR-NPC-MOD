"""Applies lotrweapons' JSON patches the way the game does (add, replace, addmerge on a "/"-path) to the files they
target, and fails if a path doesn't resolve or an added animation moves a body part the shape doesn't have.

    python patches.py <lotrweapons out dir> <LRC zip> [<game assets dir>]

Targets in lotrweapons and LRC (build-inputs/lrc-*.zip) are always checked; game: targets only when a copy of the
game's assets folder is given (it is not in the repo; e.g. Vintagestory/assets).
"""
import json
import os
import re
import sys
import zipfile

out, lrc_zip = sys.argv[1], sys.argv[2]
game = sys.argv[3] if len(sys.argv) > 3 else None
A = os.path.join(out, "assets", "lotrweapons")
lrc = zipfile.ZipFile(lrc_zip)
errors, applied, skipped = [], 0, set()


def load_json5(text):
    """The game's JSON5 enough for its own asset files: comments, unquoted keys, single quotes, trailing commas."""
    text = re.sub(r"(?m)^\s*//.*$", "", text)
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S).replace("'", '"')
    text = re.sub(r'([{,]\s*)([A-Za-z_$][\w$-]*)\s*:', r'\1"\2":', text)
    return json.loads(re.sub(r",(\s*[}\]])", r"\1", text))


def target(file):
    domain, path = file.split(":", 1)
    path = path if path.endswith(".json") else path + ".json"
    if domain == "lotrweapons":
        return load_json5(open(os.path.join(A, path), encoding="utf-8-sig").read())
    if domain == "lrc":
        return load_json5(lrc.read(f"assets/lrc/{path}").decode("utf-8-sig"))
    if domain == "game" and game:
        for sub in ("game", "survival"):
            p = os.path.join(game, sub, path)
            if os.path.exists(p):
                return load_json5(open(p, encoding="utf-8-sig").read())
        raise FileNotFoundError(file)
    return None


def apply(doc, patch):
    *parents, last = patch["path"].strip("/").split("/")
    node = doc
    for key in parents:
        node = node[int(key)] if isinstance(node, list) else next(v for k, v in node.items() if k.lower() == key.lower())
    if patch["op"] == "add" and last == "-":
        node.append(patch["value"])
    elif patch["op"] == "replace":
        if last not in node:
            raise KeyError(f"replace: {last} missing")
        node[last] = patch["value"]
    elif patch["op"] == "addmerge":
        node.setdefault(last, {}).update(patch["value"])
    else:
        node[last] = patch["value"]


docs = {}
for name in sorted(os.listdir(os.path.join(A, "patches"))):
    for patch in json.load(open(os.path.join(A, "patches", name), encoding="utf-8")):
        file = patch["file"]
        if file not in docs:
            try:
                docs[file] = target(file)
            except (KeyError, FileNotFoundError) as e:
                errors.append(f"{name}: {file} not found ({e})")
                docs[file] = None
        if docs[file] is None:
            skipped.add(file)
            continue
        try:
            apply(docs[file], patch)
            applied += 1
        except (KeyError, StopIteration, IndexError, ValueError) as e:
            errors.append(f"{name}: {patch['op']} {patch['path']} on {file}: {e!r}")

# Every element an added animation moves must be a part of that body.
for file, doc in docs.items():
    if doc is None or "/shapes/" not in file.replace(":", "/"):
        continue
    parts = set()

    def walk(elements):
        for e in elements:
            parts.add(e["name"])
            walk(e.get("children") or [])
    walk(doc["elements"])
    for anim in doc["animations"]:
        if anim["code"] in ("poleaxechop", "poleaxehold", "greatswordhold"):
            missing = {n for k in anim["keyframes"] for n in k["elements"]} - parts
            if missing:
                errors.append(f"{file}: {anim['code']} moves parts it doesn't have: {sorted(missing)}")

if errors:
    sys.exit("lotrweapons patch checks failed:\n  " + "\n  ".join(errors))
print(f"lotrweapons: {applied} patches applied" + (f", {len(skipped)} game files skipped (no game assets given)" if skipped else ""))
