#!/usr/bin/env python3
"""Download Project Gutenberg UTF-8 texts for Liber Terra MVP works."""

from __future__ import annotations

import argparse
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from mvp_works import MVP_WORKS  # noqa: E402

GUTENBERG_TXT = "https://www.gutenberg.org/ebooks/{id}.txt.utf-8"
CACHE = ROOT / "cache" / "gutenberg"


def download_one(book_id: int, filename: str, force: bool = False) -> Path:
    CACHE.mkdir(parents=True, exist_ok=True)
    path = CACHE / filename
    if path.exists() and path.stat().st_size > 0 and not force:
        print(f"cached {book_id} -> {path.name}")
        return path
    url = GUTENBERG_TXT.format(id=book_id)
    print(f"fetching {book_id} from {url}")
    req = urllib.request.Request(url, headers={"User-Agent": "LiberTerra/0.1 (personal mod build)"})
    with urllib.request.urlopen(req, timeout=180) as resp:
        data = resp.read()
    path.write_bytes(data)
    print(f"saved {path} ({len(data)} bytes)")
    return path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true", help="Re-download even if cached")
    parser.add_argument("--only", nargs="*", help="Only download these lore codes")
    args = parser.parse_args()
    works = MVP_WORKS
    if args.only:
        wanted = set(args.only)
        works = [w for w in works if w["code"] in wanted]
    failed = []
    for work in works:
        try:
            download_one(work["id"], work["filename"], force=args.force)
        except Exception as exc:
            print(f"FAIL {work['code']} ({work['id']}): {exc}", file=sys.stderr)
            failed.append(work["code"])

    # Exit non-zero so a throttled or blocked fetch stops the build instead of
    # quietly producing a catalog with volumes missing.
    if failed:
        raise SystemExit(
            f"{len(failed)} of {len(works)} downloads failed: {', '.join(failed)}"
        )


if __name__ == "__main__":
    main()
