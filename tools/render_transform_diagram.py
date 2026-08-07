#!/usr/bin/env python3
"""Draw the transform stack a book goes through on its way into a pile.

Every box in the figure is the real model, projected isometrically after the real matrices, so the
picture cannot claim an orientation the game does not produce. The three stages are the ones that
actually matter when authoring a pose: the model as the shape file defines it, the mesh after the
item's own groundStorageTransform has been baked in, and the book after a pile slot has placed it.
"""

from __future__ import annotations

import math
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from bookpile_geometry import (  # noqa: E402
    GST_ORIGIN,
    GST_ROLL,
    GST_TRANSLATE,
    GST_YAW,
    HI,
    ITEM_FROM,
    LO,
    PIVOT,
    apply,
    corners,
    mul,
    rot_x,
    rot_y,
    rot_z,
)

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "images" / "book-transforms.svg"

PAGE_BG = "#26221d"
PANEL_BG = "#332d26"
PANEL_EDGE = "#5a4f42"
INK = "#efe6d5"
SPINE = "#d9a441"
TEXT = "#cfc4b0"
FAINT = "#8a7f70"
AXIS = "#7f9bb5"

PANEL = 190
PAD = 16
GAP = 58
CAPTION = 62

# Isometric: x to the lower right, z to the lower left, y up the page.
ISO = math.cos(math.radians(30)), math.sin(math.radians(30))


def project(p, scale, cx, cy):
    x, y, z = p
    return (cx + (x - z) * ISO[0] * scale, cy + ((x + z) * ISO[1] - y) * scale)


def raw_mesh():
    """The book as block/clutter/bookshelves/small-normal defines it, in block units."""
    return [tuple((ITEM_FROM[i] + c[i]) / 16 for i in range(3)) for c in corners(LO, HI)]


def spine_face():
    """The spine sits at max Z on the raw model; four corners of that face."""
    return [
        tuple((ITEM_FROM[i] + c[i]) / 16 for i in range(3))
        for c in corners(LO, HI)
        if abs(c[2] - HI[2]) < 1e-9
    ]


def transform(points, rotation, pivot, offset=(0.0, 0.0, 0.0)):
    out = []
    for p in points:
        q = apply(rotation, tuple(p[i] - pivot[i] for i in range(3)))
        out.append(tuple(q[i] + pivot[i] + offset[i] for i in range(3)))
    return out


EDGES = [
    (0, 1), (0, 2), (0, 4), (1, 3), (1, 5), (2, 3),
    (2, 6), (3, 7), (4, 5), (4, 6), (5, 7), (6, 7),
]


def draw_box(points, spine, scale, cx, cy):
    """Wireframe box with the spine face filled, so the book's facing stays readable."""
    out = []
    flat = [project(p, scale, cx, cy) for p in points]
    if spine:
        sp = [project(p, scale, cx, cy) for p in spine]
        order = [0, 1, 3, 2]
        pts = " ".join(f"{sp[i][0]:.1f},{sp[i][1]:.1f}" for i in order)
        out.append(f'<polygon points="{pts}" fill="{SPINE}" fill-opacity="0.5" stroke="none"/>')
    for a, b in EDGES:
        out.append(
            f'<line x1="{flat[a][0]:.1f}" y1="{flat[a][1]:.1f}" '
            f'x2="{flat[b][0]:.1f}" y2="{flat[b][1]:.1f}" stroke="{INK}" stroke-width="1.3"/>'
        )
    return out


def draw_axes(scale, cx, cy):
    out = []
    origin = project((0, 0, 0), scale, cx, cy)
    for vec, label in (((0.30, 0, 0), "x"), ((0, 0.30, 0), "y"), ((0, 0, 0.30), "z")):
        tip = project(vec, scale, cx, cy)
        out.append(
            f'<line x1="{origin[0]:.1f}" y1="{origin[1]:.1f}" x2="{tip[0]:.1f}" y2="{tip[1]:.1f}" '
            f'stroke="{AXIS}" stroke-width="1" stroke-dasharray="3 2"/>'
        )
        out.append(
            f'<text x="{tip[0]:.1f}" y="{tip[1] - 3:.1f}" fill="{AXIS}" '
            f'font-family="system-ui, sans-serif" font-size="9" text-anchor="middle">{label}</text>'
        )
    return out


def main() -> int:
    raw, spine = raw_mesh(), spine_face()

    baked = mul(rot_y(GST_YAW), rot_z(GST_ROLL))
    flat = transform(raw, baked, GST_ORIGIN, GST_TRANSLATE)
    flat_spine = transform(spine, baked, GST_ORIGIN, GST_TRANSLATE)

    # A Shelved slot: stands the book back up, which needs pitch -35 as well as roll -90 because
    # the bake above yawed it 35 degrees between the tip and the slot rotation.
    slot = mul(mul(rot_y(0.0), rot_x(-35.0)), rot_z(-90.0))
    upright = transform(flat, slot, PIVOT)
    upright_spine = transform(flat_spine, slot, PIVOT)

    stages = [
        (raw, spine, "1. Shape file",
         "small-normal, standing.", "1.9 x 6.0 x 4.8 in sixteenths; spine at +Z"),
        (flat, flat_spine, "2. After groundStorageTransform",
         "Baked into the mesh by the item.", "rotate Z 90 (tip flat), then Y 35"),
        (upright, upright_spine, "3. After a pile slot",
         "Shelved stands it back up.", "yaw 0, pitch -35, roll -90"),
    ]

    scale = PANEL * 0.86
    width = PAD + len(stages) * (PANEL + GAP) - GAP + PAD
    height = PAD + PANEL + CAPTION + PAD

    out = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
        f'viewBox="0 0 {width} {height}" role="img" '
        f'aria-label="A book model through the shape file, the ground storage transform, '
        f'and a pile slot">',
        f'<rect width="100%" height="100%" rx="8" fill="{PAGE_BG}"/>',
    ]

    for index, (pts, sp, title, sub, detail) in enumerate(stages):
        ox = PAD + index * (PANEL + GAP)
        out.append(
            f'<rect x="{ox}" y="{PAD}" width="{PANEL}" height="{PANEL}" rx="6" '
            f'fill="{PANEL_BG}" stroke="{PANEL_EDGE}"/>'
        )
        cx, cy = ox + PANEL * 0.5, PAD + PANEL * 0.66
        out += draw_axes(scale, cx, cy)
        out += draw_box(pts, sp, scale, cx, cy)

        if index < len(stages) - 1:
            ax = ox + PANEL + 14
            ay = PAD + PANEL * 0.5
            out.append(
                f'<path d="M {ax} {ay} l {GAP - 30} 0 m -7 -5 l 7 5 l -7 5" fill="none" '
                f'stroke="{FAINT}" stroke-width="1.4"/>'
            )

        ty = PAD + PANEL + 20
        out.append(
            f'<text x="{ox}" y="{ty}" fill="{TEXT}" font-family="system-ui, sans-serif" '
            f'font-size="12" font-weight="600">{title}</text>'
        )
        out.append(
            f'<text x="{ox}" y="{ty + 16}" fill="{TEXT}" font-family="system-ui, sans-serif" '
            f'font-size="11">{sub}</text>'
        )
        out.append(
            f'<text x="{ox}" y="{ty + 31}" fill="{FAINT}" font-family="system-ui, sans-serif" '
            f'font-size="10">{detail}</text>'
        )

    out.append(
        f'<text x="{width - PAD}" y="{height - PAD + 2}" fill="{FAINT}" '
        f'font-family="system-ui, sans-serif" font-size="9" text-anchor="end">'
        f'gold face = spine</text>'
    )
    out.append("</svg>")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"  transforms: {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
