#!/usr/bin/env python3
"""Render the book pile layout icons for the docs, straight from the C# the game draws.

The picker icons are drawn at runtime with Cairo from the bar tables in
mod/src/Storage/BookPileLayoutIcons.cs. Parsing those same tables here means the documentation
cannot drift from what a player actually sees: change a bar, rebuild the docs, and the picture
follows. Light bars on a dark tile is the picker's own look, so the figure reads on a light or a
dark docs page without needing to know which one it is on.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ICONS_CS = ROOT / "mod" / "src" / "Storage" / "BookPileLayoutIcons.cs"
OUT = ROOT / "docs" / "images" / "bookpile-layouts.svg"

# Picker order, matching BookPileLayoutMode and the SkillItem list in the behavior.
STYLES = [
    ("Messy", "Messy pile"),
    ("Neat", "Neat stacks"),
    ("Tumbled", "Tumbled pile"),
    ("Shelved", "Upright rows"),
    ("Leaning", "Leaning books"),
    ("Uneven", "Uneven columns"),
    ("Bridged", "Bridged stacks"),
    ("Scattered", "Scattered books"),
]

TILE = 84
GAP = 14
LABEL = 34
PER_ROW = 4

INK = "#efe6d5"
TILE_BG = "#38312a"
TILE_EDGE = "#5a4f42"
PAGE_BG = "#26221d"
TEXT = "#cfc4b0"


def parse_icons(source: str) -> tuple[float, dict[str, list[tuple[float, float, float, float]]]]:
    """Pull the bar height and every *Bars table out of the C# source."""
    consts = {m[0]: float(m[1]) for m in re.findall(r"private const double (\w+) = ([\d.]+);", source)}
    bar_height = consts["BarHeight"]

    tables: dict[str, list[tuple[float, float, float, float]]] = {}
    for name, body in re.findall(r"(\w+)Bars\s*=\s*\[(.*?)\];", source, re.S):
        bars = []
        for dx, y, angle, width in re.findall(
            r"\(\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(-?[\d.]+),\s*(\w+|[\d.]+)\s*\)", body
        ):
            resolved = consts.get(width)
            bars.append((float(dx), float(y), float(angle), resolved if resolved else float(width)))
        tables[name] = bars
    return bar_height, tables


def render(bar_height: float, tables: dict[str, list[tuple[float, float, float, float]]]) -> str:
    rows = (len(STYLES) + PER_ROW - 1) // PER_ROW
    width = GAP + PER_ROW * (TILE + GAP)
    height = GAP + rows * (TILE + LABEL + GAP)

    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
        f'viewBox="0 0 {width} {height}" role="img" '
        f'aria-label="The eight book pile layout icons shown in the in-game picker">',
        f'<rect width="100%" height="100%" rx="8" fill="{PAGE_BG}"/>',
    ]

    for index, (key, label) in enumerate(STYLES):
        col, row = index % PER_ROW, index // PER_ROW
        ox = GAP + col * (TILE + GAP)
        oy = GAP + row * (TILE + LABEL + GAP)

        out.append(
            f'<rect x="{ox}" y="{oy}" width="{TILE}" height="{TILE}" rx="6" '
            f'fill="{TILE_BG}" stroke="{TILE_EDGE}"/>'
        )
        for dx, baseline, angle, bar_width in tables[key]:
            bw, bh = TILE * bar_width, TILE * bar_height
            cx, cy = ox + TILE * (0.5 + dx), oy + TILE * baseline
            out.append(
                f'<g transform="translate({cx:.2f},{cy:.2f}) rotate({angle})">'
                f'<rect x="{-bw / 2:.2f}" y="{-bh / 2:.2f}" width="{bw:.2f}" height="{bh:.2f}" '
                f'fill="{INK}"/></g>'
            )
        out.append(
            f'<text x="{ox + TILE / 2}" y="{oy + TILE + 21}" fill="{TEXT}" '
            f'font-family="system-ui, sans-serif" font-size="12" text-anchor="middle">{label}</text>'
        )

    out.append("</svg>")
    return "\n".join(out) + "\n"


def main() -> int:
    bar_height, tables = parse_icons(ICONS_CS.read_text(encoding="utf-8"))
    missing = [key for key, _ in STYLES if key not in tables]
    if missing:
        print(f"No bar table in {ICONS_CS.name} for: {', '.join(missing)}", file=sys.stderr)
        return 1

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(render(bar_height, tables), encoding="utf-8")
    print(f"  icons:    {OUT} ({len(STYLES)} layouts)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
