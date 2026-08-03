#!/usr/bin/env python3
"""Strip, paginate, and emit Vintage Story lore assets from cached Gutenberg texts."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from mvp_works import MVP_WORKS  # noqa: E402

CACHE = ROOT / "cache" / "gutenberg"
ASSETS = ROOT / "mod" / "assets" / "liberterra"
LORE_DIR = ASSETS / "config" / "lore"
LANG_PATH = ASSETS / "lang" / "en.json"
CATALOG_PATH = ASSETS / "config" / "liberterra-catalog.json"
LIBRARY_ITEM_PATH = ASSETS / "itemtypes" / "meta" / "liberterra-library.json"
RANDOMIZER_PATH = ASSETS / "itemtypes" / "meta" / "stackrandomizer-liberterra.json"
PATCHES_DIR = ASSETS / "patches"
BR_COMPAT_PATCHES_DIR = ASSETS / "compatibility" / "betterruins" / "patches"

# Soft targets matching vanilla lore scale and book limits.
PIECE_TARGET = 2000
PAGE_TARGET = 900
VOLUME_MAX_CHARS = 70_000
NEWPAGE = "___NEWPAGE___"

START_RE = re.compile(
    r"\*\*\*\s*START OF (?:THE |THIS )?PROJECT GUTENBERG[^\n]*\*\*\*",
    re.IGNORECASE,
)
END_RE = re.compile(
    r"\*\*\*\s*END OF (?:THE |THIS )?PROJECT GUTENBERG[^\n]*\*\*\*",
    re.IGNORECASE,
)


def strip_gutenberg(raw: str) -> str:
    start = START_RE.search(raw)
    end = END_RE.search(raw)
    if start and end and end.start() > start.end():
        body = raw[start.end() : end.start()]
    elif start:
        body = raw[start.end() :]
    else:
        body = raw
    # Collapse excessive blank lines; normalize newlines.
    body = body.replace("\r\n", "\n").replace("\r", "\n")
    body = re.sub(r"\n{3,}", "\n\n", body).strip()

    # Some editions use a plain footer instead of *** END OF ... ***.
    plain_end = re.search(r"(?im)^End of (?:the )?Project Gutenberg.*$", body)
    if plain_end:
        body = body[: plain_end.start()].rstrip()

    # Drop leading Project Gutenberg / PGDP boilerplate paragraphs only.
    paras = [p.strip() for p in re.split(r"\n\s*\n", body) if p.strip()]
    while paras and re.search(r"(?i)project gutenberg|pgdp\.net", paras[0]):
        paras.pop(0)
    return "\n\n".join(paras).strip()


def paragraphs(text: str) -> list[str]:
    parts = [p.strip() for p in re.split(r"\n\s*\n", text) if p.strip()]
    return parts or [text.strip()]


def pack_pages(paras: list[str], page_target: int = PAGE_TARGET) -> list[str]:
    """Join paragraphs into page-sized chunks separated later by NEWPAGE."""
    pages: list[str] = []
    buf: list[str] = []
    size = 0
    for para in paras:
        extra = len(para) + (2 if buf else 0)
        if buf and size + extra > page_target:
            pages.append("\n\n".join(buf))
            buf = [para]
            size = len(para)
        else:
            buf.append(para)
            size += extra
    if buf:
        pages.append("\n\n".join(buf))
    return pages


def pack_pieces(pages: list[str], piece_target: int = PIECE_TARGET) -> list[str]:
    pieces: list[str] = []
    buf: list[str] = []
    size = 0
    for page in pages:
        extra = len(page) + (len(NEWPAGE) if buf else 0)
        if buf and size + extra > piece_target:
            pieces.append(f"\n\n{NEWPAGE}\n\n".join(buf))
            buf = [page]
            size = len(page)
        else:
            buf.append(page)
            size += extra
    if buf:
        pieces.append(f"\n\n{NEWPAGE}\n\n".join(buf))
    return pieces


def split_volumes(pieces: list[str], max_chars: int = VOLUME_MAX_CHARS) -> list[list[str]]:
    volumes: list[list[str]] = []
    current: list[str] = []
    size = 0
    for piece in pieces:
        if current and size + len(piece) > max_chars:
            volumes.append(current)
            current = [piece]
            size = len(piece)
        else:
            current.append(piece)
            size += len(piece)
    if current:
        volumes.append(current)
    return volumes


def volume_preface(work: dict, volume_index: int, volume_count: int) -> str:
    if volume_count > 1:
        return f"{work['title']}. Volume {volume_index} of {volume_count}."
    return f"{work['title']}."


def escape_lang(value: str) -> str:
    # Store as JSON string content; writer uses json.dump.
    return value.replace("\n", "\r\n")


def build_work(work: dict) -> list[dict]:
    path = CACHE / work["filename"]
    if not path.exists():
        raise FileNotFoundError(f"Missing cached text for {work['code']}: {path}")
    raw = path.read_text(encoding="utf-8", errors="replace")
    body = strip_gutenberg(raw)
    if not body:
        raise ValueError(f"Empty body after strip: {work['code']}")

    pages = pack_pages(paragraphs(body))
    pieces = pack_pieces(pages)
    volumes = split_volumes(pieces)

    catalog_entries: list[dict] = []
    for i, vol_pieces in enumerate(volumes, start=1):
        code = work["code"] if len(volumes) == 1 else f"{work['code']}-vol{i}"
        title = work["title"] if len(volumes) == 1 else f"{work['title']} (Vol. {i})"
        # Prepend a short volume title page before the text.
        intro = volume_preface(work, i, len(volumes))
        first = f"{intro}\n\n{NEWPAGE}\n\n{vol_pieces[0]}" if vol_pieces else intro
        vol_pieces = [first] + vol_pieces[1:]

        catalog_entries.append(
            {
                "code": code,
                "baseCode": work["code"],
                "title": title,
                "group": work["group"],
                "gutenbergId": work["id"],
                "volume": i,
                "volumeCount": len(volumes),
                "pieceCount": len(vol_pieces),
                "pieces": vol_pieces,
            }
        )
    return catalog_entries


BOOK_COLORS = [
    "aged-orangebrown",
    "aged-orange",
    "aged-darkgreen",
    "aged-darkgray",
    "aged-cherryred",
    "aged-brickred",
    "aged-darkolive",
    "aged-darkbeige",
    "aged-olive",
    "aged-purpleorange",
    "aged-gray",
    "rotten-gray",
    "rotten-brown",
    "rotten-rust",
    "rotten-purple",
    "rotten-green",
]

VANILLA_LORE_VARIANTS = [
    "lore-villager",
    "lore-tobias",
    "lore-research",
    "lore-diaries",
    "lore-jonas",
]


def book_color_for(code: str) -> str:
    return BOOK_COLORS[sum(ord(ch) for ch in code) % len(BOOK_COLORS)]


def write_loot_patches(catalog: list[dict]) -> None:
    """Wire Liber Terra into bony-soil panning and ruin chest lore pools.

    World loot uses the collection stackrandomizer so any volume (not just
    vol. 1) can appear, with a random aged/rotten cover.
    """
    PATCHES_DIR.mkdir(parents=True, exist_ok=True)
    BR_COMPAT_PATCHES_DIR.mkdir(parents=True, exist_ok=True)

    collection_drop = {
        "type": "item",
        "code": "stackrandomizer-liberterra-any",
    }

    # Bony soil: rare Liber Terra find; randomizer resolves to any volume.
    pan_patches = [
        {
            "op": "add",
            "side": "Server",
            "file": "game:blocktypes/wood/pan.json",
            "path": "/attributes/panningDrops/@(bonysoil|bonysoil-.*)/-",
            "value": {
                **collection_drop,
                "chance": {"avg": 0.01, "var": 0},
            },
        }
    ]
    pan_path = PATCHES_DIR / "pan-bonysoil-liberterra.json"
    pan_path.write_text(json.dumps(pan_patches, indent=2) + "\n", encoding="utf-8")

    # Vanilla ruin chest lore randomizers (schematics place stackrandomizer-lore-*).
    # Existing entry chance is 1; Liber Terra at 0.5 => meaningful but not dominant.
    vanilla_patches = []
    for variant in VANILLA_LORE_VARIANTS:
        vanilla_patches.append(
            {
                "op": "add",
                "side": "Server",
                "file": "game:itemtypes/meta/stackrandomizer.json",
                "path": f"/attributesByType/*-{variant}/stacks/-",
                "value": {
                    **collection_drop,
                    "chance": 0.5,
                },
            }
        )
    vanilla_path = PATCHES_DIR / "stackrandomizer-vanilla-lore.json"
    vanilla_path.write_text(json.dumps(vanilla_patches, indent=2) + "\n", encoding="utf-8")

    # Better Ruins general ruin chest pool (stackrandomizer-newlore).
    # Existing entries are typically chance 10; chance 8 keeps LT as spice.
    br_patches = [
        {
            "op": "add",
            "side": "Server",
            "file": "game:itemtypes/meta/stackrandomizer-betterruins.json",
            "path": "/attributesByType/*-newlore/stacks/-",
            "value": {
                **collection_drop,
                "chance": 8,
            },
        }
    ]
    br_path = BR_COMPAT_PATCHES_DIR / "stackrandomizer-newlore.json"
    br_path.write_text(json.dumps(br_patches, indent=2) + "\n", encoding="utf-8")

    # One collection-wide stackrandomizer: any Liber Terra volume, any aged/rotten cover.
    any_stacks = [
        {
            "type": "item",
            "code": f"game:lore-book-{color}",
            "chance": 1,
            "attributes": {"category": entry["code"]},
        }
        for entry in catalog
        for color in BOOK_COLORS
    ]
    any_randomizer = {
        "code": "stackrandomizer",
        "class": "ItemStackRandomizer",
        "variantgroups": [{"code": "type", "states": ["liberterra-any"]}],
        "attributesByType": {
            "*-liberterra-any": {
                "handbook": {"exclude": True},
                "stacks": any_stacks,
            }
        },
        "maxstacksize": 1,
        "texture": {"base": "game:item/meta/randomizer/library"},
        "creativeinventory": {"liberterra": ["*"], "meta": ["*"]},
    }
    # Place under game domain so schematics/loot can use game:stackrandomizer-liberterra-any
    game_meta = ROOT / "mod" / "assets" / "game" / "itemtypes" / "meta"
    game_meta.mkdir(parents=True, exist_ok=True)
    any_path = game_meta / "stackrandomizer-liberterra-loot.json"
    any_path.write_text(json.dumps(any_randomizer, indent="\t") + "\n", encoding="utf-8")

    print(f"Wrote loot patches via collection randomizer ({len(catalog)} volumes)")
    print(f"  pan:      {pan_path}")
    print(f"  vanilla:  {vanilla_path}")
    print(f"  betterruins compat: {br_path}")
    print(f"  pooled RN: {any_path} ({len(any_stacks)} outcomes)")


def write_assets(entries: list[dict]) -> None:
    LORE_DIR.mkdir(parents=True, exist_ok=True)
    LANG_PATH.parent.mkdir(parents=True, exist_ok=True)
    LIBRARY_ITEM_PATH.parent.mkdir(parents=True, exist_ok=True)

    # Clear previously generated lore files and old per-volume randomizers.
    for old in LORE_DIR.glob("lore-*.json"):
        old.unlink()
    if RANDOMIZER_PATH.exists():
        RANDOMIZER_PATH.unlink()

    lang: dict[str, str] = {
        "game:tabname-liberterra": "Liber Terra",
        "item-library": "Liber Terra Library",
    }
    catalog_public: list[dict] = []
    creative_stacks: list[dict] = []

    for entry in entries:
        code = entry["code"]
        title = entry["title"]
        pieces = entry["pieces"]
        piece_keys = [f"liberterra:lore-{code}-piece{n}" for n in range(1, len(pieces) + 1)]
        color = book_color_for(code)

        lore_obj = {
            "code": code,
            "category": code,
            "title": f"liberterra:lore-{code}-title",
            "pieces": piece_keys,
        }
        (LORE_DIR / f"lore-{code}.json").write_text(
            json.dumps(lore_obj, indent="\t") + "\n", encoding="utf-8"
        )

        lang[f"lore-{code}-title"] = escape_lang(title)
        for n, piece in enumerate(pieces, start=1):
            lang[f"lore-{code}-piece{n}"] = escape_lang(piece)
        lang[f"game:ingamediscovery-lore-{code}"] = escape_lang(
            f"Discovered lore '{title}'\r\nPart {{0}} / {{1}}\r\n"
            f'<font size="20">Hit J to open your Journal</font>'
        )

        catalog_public.append(
            {
                "code": code,
                "baseCode": entry["baseCode"],
                "title": title,
                "group": entry["group"],
                "gutenbergId": entry["gutenbergId"],
                "volume": entry["volume"],
                "volumeCount": entry["volumeCount"],
                "pieceCount": entry["pieceCount"],
                "titleCode": f"liberterra:lore-{code}-title",
                "textCodes": piece_keys,
            }
        )

        # Real lore books in the Liber Terra creative tab (not stackrandomizers).
        creative_stacks.append(
            {
                "type": "item",
                "code": f"game:lore-book-{color}",
                "attributes": {"category": code},
            }
        )

    lang[f"game:item-stackrandomizer-liberterra-any"] = escape_lang(
        "Liber Terra: Random Library Book"
    )

    LANG_PATH.write_text(json.dumps(lang, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    CATALOG_PATH.write_text(
        json.dumps({"version": 1, "works": catalog_public}, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    # Host item: invisible itself, but its creativeinventoryStacks are real lore books.
    library_item = {
        "code": "library",
        "maxstacksize": 1,
        "attributes": {"handbook": {"exclude": True}},
        "texture": {"base": "game:item/meta/randomizer/library"},
        "creativeinventoryStacks": [
            {
                "tabs": ["liberterra"],
                "stacks": creative_stacks,
            }
        ],
    }
    LIBRARY_ITEM_PATH.write_text(json.dumps(library_item, indent="\t") + "\n", encoding="utf-8")

    write_loot_patches(catalog_public)

    print(f"Wrote {len(entries)} lore volumes")
    print(f"  lore dir: {LORE_DIR}")
    print(f"  lang:     {LANG_PATH} ({len(lang)} keys)")
    print(f"  catalog:  {CATALOG_PATH}")
    print(f"  creative: {LIBRARY_ITEM_PATH}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--only", nargs="*", help="Only build these base lore codes")
    args = parser.parse_args()
    works = MVP_WORKS
    if args.only:
        wanted = set(args.only)
        works = [w for w in works if w["code"] in wanted]

    entries: list[dict] = []
    for work in works:
        try:
            built = build_work(work)
            print(
                f"{work['code']}: {sum(e['pieceCount'] for e in built)} pieces "
                f"in {len(built)} volume(s)"
            )
            entries.extend(built)
        except Exception as exc:
            print(f"FAIL {work['code']}: {exc}", file=sys.stderr)

    if not entries:
        raise SystemExit("No lore assets generated")
    write_assets(entries)


if __name__ == "__main__":
    main()
