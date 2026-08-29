"""Fetch and flatten non-Gutenberg public-domain texts into cacheable UTF-8."""

from __future__ import annotations

import html as htmlmod
import io
import re
import subprocess
import tempfile
import zipfile
from html.parser import HTMLParser
from pathlib import Path


class _HTMLTextExtractor(HTMLParser):
    _SKIP = frozenset({"script", "style", "noscript", "head"})

    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self._skip_depth = 0
        self._chunks: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        if tag in self._SKIP:
            self._skip_depth += 1
            return
        if self._skip_depth:
            return
        if tag in {"p", "div", "br", "li", "tr", "h1", "h2", "h3", "h4", "blockquote"}:
            self._chunks.append("\n")

    def handle_endtag(self, tag: str) -> None:
        if tag in self._SKIP and self._skip_depth:
            self._skip_depth -= 1
            return
        if self._skip_depth:
            return
        if tag in {"p", "div", "li", "h1", "h2", "h3", "h4", "blockquote"}:
            self._chunks.append("\n")

    def handle_data(self, data: str) -> None:
        if not self._skip_depth:
            self._chunks.append(data)

    def text(self) -> str:
        raw = htmlmod.unescape("".join(self._chunks))
        raw = raw.replace("\r\n", "\n").replace("\r", "\n")
        raw = re.sub(r"[ \t]+\n", "\n", raw)
        raw = re.sub(r"[ \t]{2,}", " ", raw)
        return re.sub(r"\n{3,}", "\n\n", raw).strip()


def html_to_text(markup: str) -> str:
    parser = _HTMLTextExtractor()
    parser.feed(markup)
    parser.close()
    return parser.text()


def extract_span(
    text: str,
    start: str | None = None,
    end: str | None = None,
    *,
    end_re: str | None = None,
    include_end: bool = True,
) -> str:
    """Keep the body between optional start/end markers."""
    body = text
    if start:
        index = body.find(start)
        if index < 0:
            raise ValueError(f"extract_start not found: {start!r}")
        body = body[index:]
    if end:
        index = body.find(end, 1)
        if index < 0:
            raise ValueError(f"extract_end not found: {end!r}")
        body = body[: index + (len(end) if include_end else 0)]
    if end_re:
        match = re.search(end_re, body)
        if not match:
            raise ValueError(f"extract_end_re not found: {end_re!r}")
        body = body[: match.start()]
    return body.strip()


def epub_to_text(data: bytes) -> str:
    """Join numbered page_N.html files in order; used for IA scan-derived EPUBs."""
    pages: list[str] = []
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        numbered: list[tuple[int, str]] = []
        for name in archive.namelist():
            match = re.search(r"page_(\d+)\.html$", name)
            if match:
                numbered.append((int(match.group(1)), name))
        if not numbered:
            raise ValueError("epub has no page_N.html files")
        for _, name in sorted(numbered):
            markup = archive.read(name).decode("utf-8", errors="replace")
            page = html_to_text(markup).strip()
            if page:
                pages.append(page)
    return "\n\n".join(pages).strip()


def _pdftotext_bin() -> str:
    import shutil

    found = shutil.which("pdftotext")
    if found:
        return found
    for candidate in ("/opt/homebrew/bin/pdftotext", "/usr/local/bin/pdftotext"):
        if Path(candidate).is_file():
            return candidate
    raise FileNotFoundError("pdftotext")


def pdf_to_text(data: bytes) -> str:
    """Layout-preserving text via pdftotext (poppler)."""
    with tempfile.NamedTemporaryFile(suffix=".pdf") as handle:
        handle.write(data)
        handle.flush()
        try:
            result = subprocess.run(
                [_pdftotext_bin(), "-layout", "-enc", "UTF-8", handle.name, "-"],
                check=True,
                capture_output=True,
            )
        except FileNotFoundError as exc:
            raise RuntimeError(
                "pdftotext is required for PDF sources (install poppler)"
            ) from exc
        except subprocess.CalledProcessError as exc:
            stderr = exc.stderr.decode("utf-8", errors="replace")
            raise RuntimeError(f"pdftotext failed: {stderr}") from exc
    text = result.stdout.decode("utf-8", errors="replace")
    return text.replace("\f", "\n").strip()


def apply_extract(text: str, work: dict) -> str:
    text = text.replace("\r\n", "\n").replace("\r", "\n")
    return extract_span(
        text,
        start=work.get("extract_start"),
        end=work.get("extract_end"),
        end_re=work.get("extract_end_re"),
        include_end=work.get("include_end", True),
    )


def source_url(work: dict) -> str:
    if work.get("url"):
        return str(work["url"])
    book_id = work.get("id")
    if book_id:
        return f"https://www.gutenberg.org/ebooks/{book_id}"
    return ""


def cache_filename(work: dict, cache_dir: Path) -> Path:
    return cache_dir / work["filename"]
