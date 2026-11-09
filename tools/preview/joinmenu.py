"""Offline layout preview of the join menu (0.0.93; no stance tag since 0.0.94), drawn with the same geometry as GuiDialogJoinFaction and
GuiElementFactionCard at GUI scale 1. The game's fonts differ (the preview uses DejaVu Sans, a little wider than the
game's sans-serif, so wrapping here is the worst case). Portraits are placeholders: the game draws the creative-tab
portrait in that box.

usage: python3 -I joinmenu.py <generated assets/lotrnpcs/lang/en.json> <out.png>
"""
import json
import sys

from PIL import Image, ImageDraw, ImageFont

COLUMNS, CARD_W, CARD_H, GAP, HEADER_H = 3, 232, 60, 6, 28
WIDTH = COLUMNS * CARD_W + (COLUMNS - 1) * GAP
PAD = 20
PORTRAIT, INSET = 48, 4
TEXT_LEFT = INSET + PORTRAIT + 8

# (faction, followers, total, stance, bloc), as JoinSystem.Options sends them for a new config.
FACTIONS = [
    ("shire", 5, 16, "defensive", "free"), ("gondor", 5, 16, "aggressive", "free"), ("rohan", 5, 16, "aggressive", "free"),
    ("arnor", 5, 16, "aggressive", "free"), ("dale", 5, 16, "aggressive", "free"), ("dwarves", 4, 13, "aggressive", "free"),
    ("elves", 4, 13, "aggressive", "free"),
    ("mordor", 7, 22, "aggressive", "shadow"), ("angmar", 7, 22, "aggressive", "shadow"), ("mistymountains", 8, 26, "aggressive", "shadow"),
    ("gundabad", 7, 22, "aggressive", "shadow"), ("isengard", 5, 15, "aggressive", "shadow"), ("haradrim", 5, 16, "aggressive", "shadow"),
    ("easterlings", 5, 16, "aggressive", "shadow"), ("blacknumenoreans", 5, 16, "aggressive", "shadow"),
    ("dunlendings", 6, 19, "aggressive", "other"), ("wickeddwarves", 4, 13, "aggressive", "other"), ("tormentedelves", 4, 13, "aggressive", "other"),
]
BLOC_COLOR = {"free": (0.85, 0.71, 0.42), "shadow": (0.85, 0.54, 0.42), "other": (0.74, 0.68, 0.58)}


BG = (0.251, 0.208, 0.161)


def rgba(c, a=1.0, under=BG):
    """The colour c (with alpha) as drawn over under: the game blends, PIL's ImageDraw doesn't, so blend here."""
    alpha = c[3] if len(c) > 3 else a
    return tuple(int(round((v * alpha + u * (1 - alpha)) * 255)) for v, u in zip(c[:3], under)) + (255,)


def font(size, bold=False):
    return ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans" + ("-Bold" if bold else "") + ".ttf", size)


def wrap(draw, text, f, width):
    lines, line = [], ""
    for word in text.split(" "):
        test = (line + " " + word).strip()
        if line and draw.textlength(test, font=f) > width:
            lines.append(line)
            line = word
        else:
            line = test
    lines.append(line)
    return lines


def line_h(size):
    return round(size * 1.17)


def main(lang_path, out):
    lang = json.load(open(lang_path, encoding="utf-8"))
    L = lambda k, *a: lang[k].format(*a)
    folk = lambda f: lang.get("folk-" + f, f)
    title_h = 35
    rows = sum((sum(1 for x in FACTIONS if x[4] == b) + COLUMNS - 1) // COLUMNS for b in ("free", "shadow", "other"))
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    explain_lines = wrap(probe, L("join-explain"), font(16), WIDTH)
    body_h = len(explain_lines) * line_h(16) + 8 + 3 * HEADER_H + rows * (CARD_H + GAP) + 3 * 2 + 40
    img = Image.new("RGBA", (WIDTH + 2 * PAD, title_h + body_h + PAD), rgba((0.251, 0.208, 0.161, 1)))
    d = ImageDraw.Draw(img, "RGBA")
    d.rectangle([0, 0, img.width, 30], fill=rgba((0.2, 0.16, 0.12, 1)))
    d.text((10, 6), L("join-title"), font=font(16), fill=rgba((0.914, 0.867, 0.808)))
    ox, y = PAD, title_h
    small, detail = font(16), font(14)
    for i, ln in enumerate(explain_lines):
        d.text((ox, y + i * line_h(16)), ln, font=small, fill=rgba((0.914, 0.867, 0.808)))
    y += len(explain_lines) * line_h(16) + 8
    for bloc in ("free", "shadow", "other"):
        group = [x for x in FACTIONS if x[4] == bloc]
        hf = font(20, True)
        title = L("join-bloc-" + bloc)
        ty = y + 4 + (HEADER_H - 4 - line_h(20)) // 2
        d.text((ox, ty), title, font=hf, fill=rgba(BLOC_COLOR[bloc]))
        rx = ox + d.textlength(title, font=hf) + 10
        d.line([rx, ty + line_h(20) // 2, ox + WIDTH, ty + line_h(20) // 2], fill=rgba((0.42, 0.34, 0.25, 0.9)), width=1)
        y += HEADER_H
        for i, (fac, fol, tot, stance, _) in enumerate(group):
            x0, y0 = ox + (i % COLUMNS) * (CARD_W + GAP), y + (i // COLUMNS) * (CARD_H + GAP)
            d.rounded_rectangle([x0, y0, x0 + CARD_W, y0 + CARD_H], 3, fill=rgba((0, 0, 0, 0.28)), outline=rgba((0.42, 0.34, 0.25, 0.9)))
            py = y0 + (CARD_H - PORTRAIT) // 2
            card = tuple(v * 0.72 for v in BG)
            d.rounded_rectangle([x0 + INSET, py, x0 + INSET + PORTRAIT, py + PORTRAIT], 2, fill=rgba((0, 0, 0, 0.3), under=card))
            d.text((x0 + INSET + 8, py + 18), "face", font=font(11), fill=rgba((0.55, 0.48, 0.38)))
            left, width = x0 + TEXT_LEFT, CARD_W - TEXT_LEFT - 6
            nf = font(16, True)
            name = folk(fac)
            lines = wrap(d, name, nf, width)
            name_h = len(lines) * line_h(16)
            stats = L("join-follower" if fol == 1 else "join-followers", fol) + "  ·  " + L("join-total", tot)
            tag = None  # 0.0.94 (owner: "Make it just say hobbits"): no stance tag
            tag_w = d.textlength(tag, font=detail) + 8 if tag else 0
            after = tag and len(lines) == 1 and d.textlength(name, font=nf) + 6 + tag_w <= width
            block = name_h + 2 + line_h(14) + (line_h(14) + 2 if tag and not after else 0)
            top = y0 + max(2, (CARD_H - block) // 2)
            for j, ln in enumerate(lines):
                d.text((left, top + j * line_h(16)), ln, font=nf, fill=rgba((0.93, 0.88, 0.8)))
            d.text((left, top + name_h + 2), stats, font=detail, fill=rgba((0.74, 0.68, 0.58)))
            if d.textlength(stats, font=detail) > width or block > CARD_H - 2:
                print("TIGHT:", fac, round(d.textlength(stats, font=detail)), width, block)
            if tag:
                tx = left + d.textlength(name, font=nf) + 6 if after else left
                tyy = top + (name_h - line_h(14)) // 2 if after else top + name_h + 2 + line_h(14) + 2
                d.rounded_rectangle([tx, tyy, tx + tag_w, tyy + line_h(14)], 2, fill=rgba((0.24, 0.31, 0.17)))
                d.text((tx + 4, tyy), tag, font=detail, fill=rgba((0.8, 0.88, 0.67)))
        y += ((len(group) + COLUMNS - 1) // COLUMNS) * (CARD_H + GAP) + 2
    for i, ln in enumerate(wrap(d, L("join-legend"), detail, WIDTH - 150)):
        d.text((ox, y + 4 + i * line_h(14)), ln, font=detail, fill=rgba((0.74, 0.68, 0.58)))
    later = L("join-later")
    bw = d.textlength(later, font=small) + 20
    d.rounded_rectangle([ox + WIDTH - bw, y, ox + WIDTH, y + 28], 3, fill=rgba((0.33, 0.27, 0.2)), outline=rgba((0.42, 0.34, 0.25)))
    d.text((ox + WIDTH - bw + 10, y + 5), later, font=small, fill=rgba((0.914, 0.867, 0.808)))
    img.save(out)
    print(out, img.size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
