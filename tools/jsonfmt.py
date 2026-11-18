"""Writes JSON for people to read and patch (1.0.4, after an outside review: AI-made JSON was "one line" and could not
be patched by hand; owner: "Break up wordy sections with blank space").

- A list or object that fits on one line (at most WIDTH characters there) stays on one line; a longer list of plain
  values (names, item codes) puts as many on each line as fit.
- Anything longer is split, one entry per line, indented by two spaces.
- Inside a split object, a member whose value spans several lines gets a blank line after it, and before it too when
  the object has four members or more, so long blocks (an entity's client and server parts, a faction) stand apart.

Usage: from jsonfmt import dumps; dumps(data) returns the text, ending in a newline.
"""
import json

WIDTH = 100


def _one_line(value):
    return json.dumps(value, ensure_ascii=False, separators=(", ", ": "))


def _format(value, indent, key_len):
    flat = _one_line(value)
    if not isinstance(value, (dict, list)) or not value or indent + key_len + len(flat) <= WIDTH:
        return flat
    inner = indent + 2
    pad = " " * inner
    if isinstance(value, list) and not any(isinstance(v, (dict, list)) for v in value):
        # Plain values (names, item codes): as many to a line as fit.
        lines, line = [], ""
        for v in value:
            item = _one_line(v)
            if line and inner + len(line) + 2 + len(item) > WIDTH:
                lines.append(line)
                line = ""
            line = line + ", " + item if line else item
        lines.append(line)
        return "[\n" + ",\n".join(pad + l for l in lines) + "\n" + " " * indent + "]"
    if isinstance(value, list):
        items = [_format(v, inner, 0) for v in value]
        sep = ",\n\n" if any("\n" in i for i in items) and all(isinstance(v, dict) for v in value) else ",\n"
        return "[\n" + sep.join(pad + i for i in items) + "\n" + " " * indent + "]"
    parts = []
    for k, v in value.items():
        key = json.dumps(k, ensure_ascii=False) + ": "
        parts.append(key + _format(v, inner, len(key)))
    text = ""
    for i, part in enumerate(parts):
        if i:
            big = "\n" in parts[i - 1] or ("\n" in part and len(parts) >= 4)
            text += ",\n\n" if big else ",\n"
        text += pad + part
    return "{\n" + text + "\n" + " " * indent + "}"


def dumps(value):
    return _format(value, 0, 0) + "\n"
