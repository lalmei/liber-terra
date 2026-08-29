#!/usr/bin/env python3
"""Download public-domain texts for Liber Terra MVP works."""

from __future__ import annotations

import argparse
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from mvp_works import MVP_WORKS  # noqa: E402
from text_sources import apply_extract, epub_to_text, html_to_text, pdf_to_text  # noqa: E402

GUTENBERG_TXT = "https://www.gutenberg.org/ebooks/{id}.txt.utf-8"
CACHE = ROOT / "cache" / "gutenberg"
USER_AGENT = "LiberTerra/0.1 (personal mod build)"


def fetch_bytes(url: str, timeout: int = 180) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return response.read()


def flatten_source(work: dict, data: bytes) -> str:
    source = work.get("source", "gutenberg")
    if source == "gutenberg":
        return data.decode("utf-8", errors="replace")
    if source == "txt":
        return apply_extract(data.decode("utf-8", errors="replace"), work)
    if source == "html":
        return apply_extract(html_to_text(data.decode("utf-8", errors="replace")), work)
    if source == "epub":
        return apply_extract(epub_to_text(data), work)
    if source == "pdf":
        return apply_extract(pdf_to_text(data), work)
    raise ValueError(f"Unknown source type {source!r} for {work['code']}")


def download_one(work: dict, force: bool = False) -> Path:
    CACHE.mkdir(parents=True, exist_ok=True)
    path = CACHE / work["filename"]
    if path.exists() and path.stat().st_size > 0 and not force:
        print(f"cached {work['code']} -> {path.name}")
        return path

    source = work.get("source", "gutenberg")
    if source == "gutenberg":
        url = GUTENBERG_TXT.format(id=work["id"])
    else:
        url = work["url"]
    print(f"fetching {work['code']} from {url}")
    data = fetch_bytes(url)
    text = flatten_source(work, data)
    if not text.strip():
        raise ValueError(f"Empty body after extract: {work['code']}")
    path.write_text(text, encoding="utf-8")
    print(f"saved {path} ({path.stat().st_size} bytes)")
    return path


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--force", action="store_true", help="Re-download even if cached")
    parser.add_argument("--only", nargs="*", help="Only download these lore codes")
    args = parser.parse_args()
    works = MVP_WORKS
    if args.only:
        wanted = set(args.only)
        works = [work for work in works if work["code"] in wanted]
    failed = []
    for work in works:
        try:
            download_one(work, force=args.force)
        except Exception as exc:
            print(f"FAIL {work['code']}: {exc}", file=sys.stderr)
            failed.append(work["code"])

    # Exit non-zero so a throttled or blocked fetch stops the build instead of
    # quietly producing a catalog with volumes missing.
    if failed:
        raise SystemExit(
            f"{len(failed)} of {len(works)} downloads failed: {', '.join(failed)}"
        )


if __name__ == "__main__":
    main()
