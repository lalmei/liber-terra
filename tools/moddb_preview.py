#!/usr/bin/env python3
"""Render docs/moddb-description.html the way the Vintage Story mod page will.

Two jobs:

* The default mode wraps the description in page chrome that imitates mods.vintagestory.at
  (parchment page, working spoiler toggles) and resolves the ``<!-- image: path -->``
  markers to the real screenshots, so the layout can be checked before pasting.
* ``--paste`` prints the description alone, header comment stripped, which is what goes
  into the editor's source view.
"""

from __future__ import annotations

import argparse
import html
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE = ROOT / "docs" / "moddb-description.html"
IMAGE_MAP = ROOT / "docs" / "moddb-images.json"

# The caption separator must have whitespace on both sides. Without that boundary, the first
# hyphen in a filename such as ``i-can-carry-books.png`` is mistaken for the separator.
IMAGE_MARKER = re.compile(r"<!--\s*image:\s*(\S+)(?:\s+[-—]\s+(.*?))?\s*-->")

PAGE = """<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>Liber Terra — ModDB description preview</title>
<style>
  body {{
    margin: 0;
    padding: 32px 16px;
    background: #cdc6b6;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  }}
  .spoiler-toggle {{ cursor: pointer; user-select: none; }}
  .spoiler-text {{ display: none; }}
  .spoiler.is-open .spoiler-text {{ display: block; }}
  img {{ max-width: 100%; height: auto; border-radius: 4px; }}
  figure {{ margin: 0 0 16px; }}
  figcaption {{ font-size: 0.85em; opacity: 0.7; }}
  .preview-bar {{
    max-width: 960px;
    margin: 0 auto 12px;
    font-size: 0.85em;
    color: #4a4436;
  }}
  .preview-bar button {{ font: inherit; margin-left: 8px; }}
</style>
</head>
<body>
<div class="preview-bar">
  Local preview — spoilers behave as they do on the mod page.
  <button type="button" data-all="open">Expand all</button>
  <button type="button" data-all="close">Collapse all</button>
</div>
{body}
<script>
  document.querySelectorAll('.spoiler-toggle').forEach(function (toggle) {{
    toggle.addEventListener('click', function () {{
      toggle.closest('.spoiler').classList.toggle('is-open');
    }});
  }});
  document.querySelectorAll('.preview-bar button').forEach(function (button) {{
    button.addEventListener('click', function () {{
      var open = button.dataset.all === 'open';
      document.querySelectorAll('.spoiler').forEach(function (spoiler) {{
        spoiler.classList.toggle('is-open', open);
      }});
    }});
  }});
</script>
</body>
</html>
"""


def description(text: str) -> str:
    """Return the description with its leading maintenance comment removed."""
    if text.lstrip().startswith("<!--"):
        text = text[text.index("-->") + len("-->") :]
    return text.strip() + "\n"


def resolve_images(text: str, base: Path) -> str:
    """Replace repository image markers with paths usable by the local preview."""

    def replace(match: re.Match[str]) -> str:
        path, caption = match.group(1), (match.group(2) or "").strip()
        target = ROOT / path
        if not target.exists():
            return '<p><em>missing image: %s</em></p>' % path
        src = os.path.relpath(target, base)
        figure = '<figure><img src="%s" alt="%s" />' % (
            src,
            caption.replace('"', "&quot;"),
        )
        if caption:
            figure += "<figcaption>%s</figcaption>" % caption
        return figure + "</figure>"

    return IMAGE_MARKER.sub(replace, text)


def load_image_map(path: Path = IMAGE_MAP) -> dict[str, str]:
    """Load repository screenshot paths mapped to their permanent ModDB CDN uploads."""
    mapping = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(mapping, dict) or not all(
        isinstance(local, str) and isinstance(remote, str) for local, remote in mapping.items()
    ):
        raise ValueError(f"ModDB image map must be a string-to-string object: {path}")
    return mapping


def resolve_remote_images(text: str, mapping: dict[str, str] | None = None) -> str:
    """Replace every image marker with its ModDB-hosted production image."""
    mapping = load_image_map() if mapping is None else mapping
    missing: list[str] = []

    def replace(match: re.Match[str]) -> str:
        path, caption = match.group(1), (match.group(2) or "").strip()
        remote = mapping.get(path)
        if remote is None:
            missing.append(path)
            return match.group(0)
        return '<p><img src="%s" alt="%s" style="max-width: 100%%; height: auto;" /></p>' % (
            html.escape(remote, quote=True),
            html.escape(caption, quote=True),
        )

    rendered = IMAGE_MARKER.sub(replace, text)
    if missing:
        raise ValueError(
            "No ModDB CDN URL for image marker(s): " + ", ".join(sorted(set(missing)))
        )
    return rendered


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--paste", action="store_true", help="print the paste-ready description")
    parser.add_argument("--out", type=Path, default=ROOT / "dist" / "moddb-preview.html")
    args = parser.parse_args()

    body = description(SOURCE.read_text(encoding="utf-8"))

    if args.paste:
        sys.stdout.write(resolve_remote_images(body))
        return 0

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(PAGE.format(body=resolve_images(body, args.out.parent)), encoding="utf-8")
    print(args.out)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
