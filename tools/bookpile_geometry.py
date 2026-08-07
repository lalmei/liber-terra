"""Where a book pile slot actually puts a book.

Mirrors what the game does at runtime, so the documentation figures show real geometry rather than
a sketch of it. Two transforms stack up:

1. The lore book's own ``groundStorageTransform`` is baked into the mesh before the pile ever sees
   it — rotate Z 90 to tip the book flat, then Y 35, then a small translate.
2. ``BlockEntityBookPile.genTransformationMatrices`` then applies
   ``Translate(x,y,z) . RotY(yaw) . RotX(pitch) . RotZ(roll) . Translate(-0.5, 0, -0.5)``.

So a slot's yaw/pitch/roll are not the book's world angles: they are whatever cancels step 1 and
lands on the pose wanted. That is why an upright book needs pitch -35 and roll -90 rather than a
plain roll -90 — the baked-in Y 35 sits between the tip and the slot rotation.
"""

from __future__ import annotations

import math

DEG = math.pi / 180.0

# The book model, from block/clutter/bookshelves/small-normal.
LO, HI = (1.6, -0.3, -2.2), (3.5, 5.7, 2.6)
ITEM_FROM = (5.5, 0.3, 8.0)

# groundStorageTransform for lore-book-* and book-*, identical for both families.
GST_ORIGIN = (0.5, 0.05, 0.5)
GST_TRANSLATE = (0.12, 0.0, -0.04)
GST_YAW = 35.0
GST_ROLL = 90.0

# What Translate(-0.5, 0, -0.5) puts the slot rotation through.
PIVOT = (0.5, 0.0, 0.5)

THICK = 1.9 / 16      # a book lying flat
TALL = 6.0 / 16       # a book standing upright
DEEP = 4.8 / 16       # front to back


def rot_x(a):
    c, s = math.cos(a * DEG), math.sin(a * DEG)
    return ((1, 0, 0), (0, c, -s), (0, s, c))


def rot_y(a):
    c, s = math.cos(a * DEG), math.sin(a * DEG)
    return ((c, 0, s), (0, 1, 0), (-s, 0, c))


def rot_z(a):
    c, s = math.cos(a * DEG), math.sin(a * DEG)
    return ((c, -s, 0), (s, c, 0), (0, 0, 1))


def mul(a, b):
    return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)) for i in range(3))


def apply(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))


def corners(lo, hi):
    return [(x, y, z) for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]


def _mesh():
    """The book mesh as the block entity receives it, with the ground transform already applied."""
    baked = mul(rot_y(GST_YAW), rot_z(GST_ROLL))
    out = []
    for c in corners(LO, HI):
        p = [(ITEM_FROM[i] + c[i]) / 16 for i in range(3)]
        q = apply(baked, [p[i] - GST_ORIGIN[i] for i in range(3)])
        out.append(tuple(q[i] + GST_ORIGIN[i] + GST_TRANSLATE[i] for i in range(3)))
    return out


MESH = _mesh()


def render(slot):
    """The eight world-space corners of the book a slot produces, in block units."""
    rot = mul(mul(rot_y(slot["yawDeg"]), rot_x(slot["pitchDeg"])), rot_z(slot["rollDeg"]))
    out = []
    for p in MESH:
        q = apply(rot, tuple(p[i] - PIVOT[i] for i in range(3)))
        out.append((q[0] + slot["x"], q[1] + slot["y"], q[2] + slot["z"]))
    return out


def bbox(points):
    return (
        [min(p[k] for p in points) for k in range(3)],
        [max(p[k] for p in points) for k in range(3)],
    )


def hull(points):
    """Convex hull of 2D points, for drawing a rotated box as one outline."""
    points = sorted(set(points))
    if len(points) < 3:
        return points

    def half(seq):
        out = []
        for p in seq:
            while len(out) >= 2:
                (ax, ay), (bx, by) = out[-2], out[-1]
                if (bx - ax) * (p[1] - ay) - (by - ay) * (p[0] - ax) > 0:
                    break
                out.pop()
            out.append(p)
        return out

    return half(points)[:-1] + half(points[::-1])[:-1]
