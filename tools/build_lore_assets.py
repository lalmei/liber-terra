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
# Hand-written UI strings, merged into the generated en.json above.
#
# They live outside assets/ on purpose. Vintage Story treats every file in a lang folder as a
# locale named after the file, so an en-bookpile.json sitting next to en.json is read as the
# locale "en-bookpile" and never loads for an English player — which is how the whole book pile
# UI ended up rendering as raw keys. Keeping the partials here makes that impossible.
LANG_OVERLAY_DIR = ROOT / "mod" / "lang"
# Locale codes Vintage Story ships, from assets/game/lang (1.20). A file in a lang folder must be
# named for one of these or the game registers it as a language nobody has selected and every
# string inside it vanishes without a word. No pattern can catch that: "en-bookpile" is built
# exactly like the real "pt-br", "es-419" and "sv-se". Add here when the game adds a locale.
VS_LOCALES = frozenset(
    {
        "ar", "be", "bg", "cs", "da", "de", "en", "eo", "es-419", "es-es",
        "fi", "fr", "hu", "is", "it", "ja", "ko", "lt", "nl", "no",
        "pl", "pt-br", "pt-pt", "ro", "ru", "sk", "sr", "sv-se", "th", "tr",
        "uk", "vi", "zh-cn", "zh-tw",
    }
)
CATALOG_PATH = ASSETS / "config" / "liberterra-catalog.json"
LIBRARY_ITEM_PATH = ASSETS / "itemtypes" / "meta" / "liberterra-library.json"
RANDOMIZER_PATH = ASSETS / "itemtypes" / "meta" / "stackrandomizer-liberterra.json"
PATCHES_DIR = ASSETS / "patches"
BR_COMPAT_PATCHES_DIR = ASSETS / "compatibility" / "betterruins" / "patches"

# Soft targets matching vanilla lore scale and book limits.
PIECE_TARGET = 2000
VOLUME_MAX_CHARS = 70_000

# Gutenberg texts arrive hard-wrapped near column 70. GuiDialogReadonlyBook
# re-wraps them to its own box (400 units, ~45 characters), so every source line
# renders as one full line plus a short remainder — ragged all the way down. We
# undo the breaks the source margin forced and let the GUI wrap instead. Verse
# keeps its own lines: those breaks are the author's, not the wrapper's.
VERSE_FILL_MAX = 58  # p90 source line length below this => verse-dominant work
VERSE_CAPS_SHARE = 0.60  # capitalised continuation lines => verse-dominant work
MIN_FORCED_SHARE = 0.5  # per paragraph, below this => deliberate line breaks

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


def _line_lengths(text: str) -> list[int]:
    return sorted(len(line.rstrip()) for line in text.split("\n") if line.strip())


def wrap_column(text: str) -> int:
    """The source's fill column: p95 of line length, so one long line can't skew it."""
    lens = _line_lengths(text)
    return lens[int(len(lens) * 0.95)] if lens else 0


def capitalised_line_share(text: str) -> float:
    """Share of continuation lines starting with a capital — the hallmark of verse."""
    caps = total = 0
    for para in text.split("\n\n"):
        for line in para.split("\n")[1:]:
            first = line.strip()[:1]
            if first.isalpha():
                total += 1
                caps += first.isupper()
    return caps / total if total else 0.0


def keeps_line_breaks(text: str, work: dict | None = None) -> bool:
    """True when the line breaks belong to the author rather than the wrapper."""
    if work and work.get("keep_line_breaks"):
        return True
    lens = _line_lengths(text)
    if not lens:
        return True
    if lens[int(len(lens) * 0.90)] < VERSE_FILL_MAX:
        return True
    return capitalised_line_share(text) >= VERSE_CAPS_SHARE


def unwrap_paragraph(para: str, column: int) -> str:
    """Join the lines the source margin forced apart; leave deliberate breaks alone."""
    lines = [line.rstrip() for line in para.split("\n")]
    if len(lines) < 2:
        return para.strip()

    # A break is the wrapper's doing when the next word could not have fitted.
    forced = [
        len(a) + 1 + len(b.strip().split(" ")[0]) > column for a, b in zip(lines, lines[1:])
    ]
    if sum(forced) < len(forced) * MIN_FORCED_SHARE:
        return para  # verse, a table of contents, a list — keep it as written

    out = [lines[0].strip()]
    for line, join in zip(lines[1:], forced):
        if join:
            out[-1] += " " + line.strip()
        else:
            out.append(line.strip())
    return "\n".join(out)


def reflow(text: str, work: dict | None = None) -> str:
    """Undo the source's hard wrapping so the book GUI can wrap to its own width."""
    if keeps_line_breaks(text, work):
        return text
    column = wrap_column(text)
    return "\n\n".join(unwrap_paragraph(para, column) for para in text.split("\n\n"))


def pack_pieces(paras: list[str], piece_target: int = PIECE_TARGET) -> list[str]:
    """Group paragraphs into lang entries of roughly piece_target characters."""
    pieces: list[str] = []
    buf: list[str] = []
    size = 0
    for para in paras:
        extra = len(para) + (2 if buf else 0)
        if buf and size + extra > piece_target:
            pieces.append("\n\n".join(buf))
            buf = [para]
            size = len(para)
        else:
            buf.append(para)
            size += extra
    if buf:
        pieces.append("\n\n".join(buf))
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

    pieces = pack_pieces(paragraphs(reflow(body, work)))
    volumes = split_volumes(pieces)

    catalog_entries: list[dict] = []
    for i, vol_pieces in enumerate(volumes, start=1):
        code = work["code"] if len(volumes) == 1 else f"{work['code']}-vol{i}"
        title = work["title"] if len(volumes) == 1 else f"{work['title']} (Vol. {i})"
        # Open each volume with its title.
        intro = volume_preface(work, i, len(volumes))
        first = f"{intro}\n\n{vol_pieces[0]}" if vol_pieces else intro
        vol_pieces = [first] + vol_pieces[1:]
        # The GUI joins consecutive pieces with a single "\n", which would run the
        # last paragraph of one into the first of the next. End each piece with a
        # newline so that seam reads as a paragraph break like any other.
        vol_pieces = [p + "\n" for p in vol_pieces[:-1]] + vol_pieces[-1:]

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

    World loot names the collection stackrandomizer so any volume (not just
    vol. 1) can appear, with a random aged/rotten cover.

    That name is a marker, not something the game can roll on its own: Vintage
    Story resolves loot once per slot, so a randomizer nested in a randomizer
    stays an item in the chest, and the pan resolves nothing at all. The mod
    replaces every marker with real books at AssetsFinalize — see
    mod/src/Loot/LiberTerraLootTables.cs before changing what is emitted here.
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


def stray_lang_files(lang_dir: Path | None = None) -> list[str]:
    """Shipped lang files Vintage Story would not load as a language.

    Real translations (de.json, pt-br.json) are fine; anything else is a string file that will
    never reach a player and belongs in LANG_OVERLAY_DIR instead.
    """
    lang_dir = LANG_PATH.parent if lang_dir is None else lang_dir
    return sorted(p.name for p in lang_dir.glob("*.json") if p.stem not in VS_LOCALES)


def check_shipped_lang_files(lang_dir: Path | None = None) -> None:
    stray = stray_lang_files(lang_dir)
    if stray:
        overlay = LANG_OVERLAY_DIR.relative_to(ROOT)
        raise ValueError(
            f"Lang files Vintage Story will not load as a language: {', '.join(stray)}. "
            f"Only locale-named files belong in assets; put partial string files in "
            f"{overlay}/en-*.json, which the build merges into en.json."
        )


def load_lang_overlays(overlay_dir: Path = LANG_OVERLAY_DIR) -> dict[str, str]:
    """Merge the hand-written en-*.json UI strings, newest-sorted for a stable order."""
    merged: dict[str, str] = {}
    seen: dict[str, str] = {}
    for path in sorted(overlay_dir.glob("en-*.json")):
        entries = json.loads(path.read_text(encoding="utf-8"))
        for key, value in entries.items():
            if key in seen:
                raise ValueError(
                    f"Duplicate lang key {key!r} in {path.name}; already set by {seen[key]}"
                )
            seen[key] = path.name
            merged[key] = value
    return merged


def apply_lang_overlays(lang: dict[str, str], overlay_dir: Path = LANG_OVERLAY_DIR) -> None:
    """Fold the overlays in, refusing to shadow a generated lore key."""
    for key, value in load_lang_overlays(overlay_dir).items():
        if key in lang:
            raise ValueError(f"Lang overlay key {key!r} collides with a generated key")
        lang[key] = value


def write_assets(entries: list[dict]) -> None:
    LORE_DIR.mkdir(parents=True, exist_ok=True)
    LANG_PATH.parent.mkdir(parents=True, exist_ok=True)
    LIBRARY_ITEM_PATH.parent.mkdir(parents=True, exist_ok=True)

    # Clear previously generated lore files and old per-volume randomizers.
    for old in LORE_DIR.glob("lore-*.json"):
        old.unlink()
    if RANDOMIZER_PATH.exists():
        RANDOMIZER_PATH.unlink()

    # Everything else the UI says lives in mod/lang/en-*.json and is merged in below.
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

    apply_lang_overlays(lang)
    LANG_PATH.write_text(json.dumps(lang, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    check_shipped_lang_files()
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
