#!/usr/bin/env python3
"""Render each book pile layout as a plan and elevation, straight from the shipped config.

Every book is drawn from the pose the game will actually use, so this figure cannot claim a shape
the block does not build. Books are shaded light to dark in fill order, which makes it readable as
"where does the next book go" and not only "what does a full pile look like".
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from bookpile_geometry import bbox, hull, render  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "mod" / "assets" / "liberterra" / "config" / "bookpile-layout.json"
OUT = ROOT / "docs" / "images" / "bookpile-shapes.svg"

LABELS = {
    "messy": "Messy",
    "neat": "Neat",
    "tumbled": "Tumbled",
    "shelved": "Shelved",
    "leaning": "Leaning",
    "uneven": "Uneven",
    "bridged": "Bridged",
    "scattered": "Scattered",
}

VIEW = 116
GAP = 8
PAD = 13
LABEL = 30
PER_ROW = 4

PAGE_BG = "#26221d"
CELL_BG = "#332d26"
CELL_EDGE = "#5a4f42"
INK = "239,230,213"
TEXT = "#cfc4b0"
FAINT = "#8a7f70"


def draw_view(slots, ox, oy, project, caption):
    out = [
        f'<rect x="{ox}" y="{oy}" width="{VIEW}" height="{VIEW}" fill="{CELL_BG}" stroke="{CELL_EDGE}"/>'
    ]
    horizontal, vertical = project
    for index, slot in enumerate(slots):
        flat = [(p[horizontal], p[vertical]) for p in render(slot)]
        outline = hull(flat)
        if project == (0, 2):                       # plan: z runs down the page
            pts = [(ox + u * VIEW, oy + v * VIEW) for u, v in outline]
        else:                                       # elevation: y runs up the page
            pts = [(ox + u * VIEW, oy + VIEW - v * VIEW) for u, v in outline]
        shade = 0.22 + 0.30 * index / max(1, len(slots) - 1)
        out.append(
            '<polygon points="' + " ".join(f"{x:.1f},{y:.1f}" for x, y in pts) + '" '
            f'fill="rgba({INK},{shade:.2f})" stroke="rgba({INK},0.85)" stroke-width="0.7"/>'
        )
    out.append(
        f'<text x="{ox + 5}" y="{oy + 12}" fill="{FAINT}" '
        f'font-family="system-ui, sans-serif" font-size="9">{caption}</text>'
    )
    return out


def main() -> int:
    config = json.loads(CONFIG.read_text(encoding="utf-8"))
    styles = [k for k in LABELS if k in config] + [k for k in config if k not in LABELS]

    rows = (len(styles) + PER_ROW - 1) // PER_ROW
    cell_w = VIEW + PAD
    cell_h = 2 * VIEW + GAP + LABEL + PAD
    width = PAD + PER_ROW * cell_w
    height = PAD + rows * cell_h

    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
        f'viewBox="0 0 {width} {height}" role="img" '
        f'aria-label="Plan and elevation of each book pile layout at full capacity">',
        f'<rect width="100%" height="100%" rx="8" fill="{PAGE_BG}"/>',
    ]

    for index, style in enumerate(styles):
        slots = config[style]
        ox = PAD + (index % PER_ROW) * cell_w
        oy = PAD + (index // PER_ROW) * cell_h
        out += draw_view(slots, ox, oy, (0, 2), "from above")
        out += draw_view(slots, ox, oy + VIEW + GAP, (0, 1), "from the side")
        out.append(
            f'<text x="{ox + VIEW / 2}" y="{oy + 2 * VIEW + GAP + 20}" fill="{TEXT}" '
            f'font-family="system-ui, sans-serif" font-size="12" text-anchor="middle">'
            f'{LABELS.get(style, style)}</text>'
        )

    out.append("</svg>")
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")

    tallest = max(bbox([p for s in config[st] for p in render(s)])[1][1] for st in styles)
    print(f"  shapes:   {OUT} ({len(styles)} layouts, {len(config[styles[0]])} books, "
          f"tallest pile {tallest:.3f} blocks)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
