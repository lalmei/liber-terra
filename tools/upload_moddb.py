#!/usr/bin/env python3
"""Upload a Liber Terra release zip to the Vintage Story Mod DB.

The official ModDB API does not yet implement release creation
(PUT /api/v2/mods/{modid}/releases/new is "Not implemented"), so this
script drives the same authenticated web form the site uses.

Auth
----
Log into https://mods.vintagestory.at in a browser, then copy the
``vs_websessionkey`` cookie value and export it:

  export VSMODDB_SESSION='...'

Or put it in a local ``.env`` file (gitignored)::

  VSMODDB_SESSION=...

Examples
--------
  make package
  export VSMODDB_SESSION='...'
  python3 tools/upload_moddb.py --changelog-file notes.md

  python3 tools/upload_moddb.py --dry-run --changelog "Book piles + stacks"
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from html.parser import HTMLParser
from pathlib import Path
from typing import Iterable
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
MODINFO_PATH = ROOT / "mod" / "modinfo.json"
DIST_DIR = ROOT / "dist"
ENV_PATH = ROOT / ".env"

MODDB_BASE = "https://mods.vintagestory.at"
DEFAULT_MOD_ID = "liberterra"
ASSETTYPE_RELEASE = 2
USER_AGENT = "LiberTerra-upload-moddb/1.0 (+https://github.com/lalmei/liber-terra)"


@dataclass
class ModInfo:
    modid: str
    name: str
    version: str
    game_dependency: str


@dataclass
class EditPage:
    action_token: str
    hovering_file_ids: list[str]


class _EditPageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.action_token: str | None = None
        self.hovering_file_ids: list[str] = []
        self._in_script = False
        self._script_chunks: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        ad = dict(attrs)
        if tag == "input":
            name = ad.get("name") or ""
            value = ad.get("value") or ""
            if name == "at" and value:
                self.action_token = value
            elif name == "fileIds[]" and value:
                self.hovering_file_ids.append(value)
        if tag == "script":
            self._in_script = True
            self._script_chunks = []

    def handle_endtag(self, tag: str) -> None:
        if tag == "script" and self._in_script:
            self._in_script = False
            text = "".join(self._script_chunks)
            m = re.search(r'actiontoken\s*=\s*"([0-9a-fA-F]+)"', text)
            if m and not self.action_token:
                self.action_token = m.group(1)

    def handle_data(self, data: str) -> None:
        if self._in_script:
            self._script_chunks.append(data)


class _MessageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.errors: list[str] = []
        self._capture = False
        self._buf: list[str] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        classes = (dict(attrs).get("class") or "").split()
        if "bg-error" in classes or "text-error" in classes:
            self._capture = True
            self._buf = []

    def handle_endtag(self, tag: str) -> None:
        if self._capture and tag in {"div", "p", "li", "span", "section"}:
            text = " ".join("".join(self._buf).split())
            if text:
                self.errors.append(text)
            self._capture = False
            self._buf = []

    def handle_data(self, data: str) -> None:
        if self._capture:
            self._buf.append(data)


def load_dotenv(path: Path = ENV_PATH) -> None:
    if not path.is_file():
        return
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue
        key, _, value = line.partition("=")
        key = key.strip()
        value = value.strip().strip("'").strip('"')
        if key and key not in os.environ:
            os.environ[key] = value


def read_modinfo(path: Path = MODINFO_PATH) -> ModInfo:
    data = json.loads(path.read_text(encoding="utf-8"))
    deps = data.get("dependencies") or {}
    game = deps.get("game")
    if not game:
        raise SystemExit(f"No game dependency in {path}")
    return ModInfo(
        modid=data["modid"],
        name=data["name"],
        version=data["version"],
        game_dependency=str(game),
    )


def default_package_path(version: str) -> Path:
    return DIST_DIR / f"LiberTerra-{version}.zip"


def parse_semver(version: str) -> tuple[int, int, int, str, int] | None:
    m = re.fullmatch(
        r"(\d+)\.(\d+)\.(\d+)(?:-(dev|pre|rc)\.(\d+))?",
        version,
    )
    if not m:
        return None
    kind = m.group(4) or ""
    pre = int(m.group(5) or 0)
    return int(m.group(1)), int(m.group(2)), int(m.group(3)), kind, pre


def version_ge(a: str, b: str) -> bool:
    pa, pb = parse_semver(a), parse_semver(b)
    if pa is None or pb is None:
        return False
    # Stable (no pre) sorts after pre/rc/dev of same x.y.z.
    kind_rank = {"": 3, "rc": 2, "pre": 1, "dev": 0}
    ta = (pa[0], pa[1], pa[2], kind_rank[pa[3]], pa[4] if pa[3] else 0)
    tb = (pb[0], pb[1], pb[2], kind_rank[pb[3]], pb[4] if pb[3] else 0)
    return ta >= tb


class ModDbClient:
    def __init__(self, session: str, base: str = MODDB_BASE) -> None:
        self.base = base.rstrip("/")
        self.session = session
        self.opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor())

    def _headers(self, extra: dict[str, str] | None = None) -> dict[str, str]:
        headers = {
            "User-Agent": USER_AGENT,
            "Cookie": f"vs_websessionkey={self.session}",
            "Accept": "text/html,application/json,*/*",
        }
        if extra:
            headers.update(extra)
        return headers

    def request(
        self,
        method: str,
        path: str,
        *,
        data: bytes | None = None,
        headers: dict[str, str] | None = None,
        allow_redirects: bool = True,
    ) -> tuple[int, dict[str, str], bytes]:
        url = path if path.startswith("http") else f"{self.base}{path}"
        req = urllib.request.Request(url, data=data, method=method, headers=self._headers(headers))
        try:
            with self.opener.open(req, timeout=180) as resp:
                body = resp.read()
                status = resp.getcode() or 200
                resp_headers = {k.lower(): v for k, v in resp.headers.items()}
                return status, resp_headers, body
        except urllib.error.HTTPError as exc:
            body = exc.read()
            resp_headers = {k.lower(): v for k, v in (exc.headers.items() if exc.headers else [])}
            if allow_redirects and exc.code in {301, 302, 303, 307, 308}:
                location = resp_headers.get("location")
                if location:
                    next_url = urllib.parse.urljoin(url, location)
                    return self.request("GET", next_url, allow_redirects=True)
            return exc.code, resp_headers, body

    def api_json(self, path: str) -> dict:
        status, _, body = self.request("GET", path, headers={"Accept": "application/json"})
        if status != 200:
            raise SystemExit(f"ModDB API {path} failed: HTTP {status}")
        return json.loads(body.decode("utf-8"))

    def get_mod(self, mod_id: str) -> dict:
        payload = self.api_json(f"/api/mod/{urllib.parse.quote(mod_id)}")
        if payload.get("statuscode") not in (200, "200"):
            raise SystemExit(f"Mod not found: {mod_id} ({payload})")
        return payload["mod"]

    def list_game_versions(self) -> list[str]:
        payload = self.api_json("/api/gameversions")
        return [g["name"] for g in payload.get("gameversions", [])]

    def fetch_edit_page(self, numeric_mod_id: int) -> EditPage:
        status, _, body = self.request("GET", f"/edit/release/?modid={numeric_mod_id}")
        html = body.decode("utf-8", errors="replace")
        if status in {401, 403} or "/login" in html and "actiontoken" not in html:
            raise SystemExit(
                "Not authenticated. Export a fresh vs_websessionkey cookie as VSMODDB_SESSION "
                "after logging into https://mods.vintagestory.at"
            )
        if status != 200:
            raise SystemExit(f"Failed to open edit-release page: HTTP {status}")

        parser = _EditPageParser()
        parser.feed(html)
        if not parser.action_token:
            raise SystemExit(
                "Could not find action token on edit-release page. "
                "Session may be expired or the page layout changed."
            )
        return EditPage(action_token=parser.action_token, hovering_file_ids=parser.hovering_file_ids)

    def delete_file(self, file_id: str, action_token: str) -> None:
        body = urllib.parse.urlencode({"fileid": file_id, "at": action_token}).encode()
        status, _, resp = self.request(
            "POST",
            "/edit-deletefile",
            data=body,
            headers={"Content-Type": "application/x-www-form-urlencoded", "Accept": "application/json"},
            allow_redirects=False,
        )
        if status != 200:
            raise SystemExit(f"Failed to delete hovering file {file_id}: HTTP {status} {resp[:300]!r}")
        payload = json.loads(resp.decode("utf-8"))
        if payload.get("status") != "ok":
            raise SystemExit(f"Failed to delete hovering file {file_id}: {payload}")

    def upload_release(
        self,
        *,
        numeric_mod_id: int,
        action_token: str,
        zip_path: Path,
        changelog_html: str,
        game_versions: Iterable[str],
    ) -> None:
        fields: list[tuple[str, str]] = [
            ("at", action_token),
            ("save", "1"),
            ("saveandback", "1"),
            ("text", changelog_html),
        ]
        for gv in game_versions:
            fields.append(("cgvs[]", gv))

        content_type, payload = encode_multipart(fields, zip_path)
        status, headers, body = self.request(
            "POST",
            f"/edit/release/?modid={numeric_mod_id}",
            data=payload,
            headers={"Content-Type": content_type},
            allow_redirects=True,
        )
        html = body.decode("utf-8", errors="replace")
        msg = _MessageParser()
        msg.feed(html)

        # Success usually lands on the mod page (#tab-files) after save+back.
        location_ok = "edit/release" not in (headers.get("location") or "")
        still_on_edit = 'name="form1"' in html and "Add new Release" in html
        if status in {200, 302, 303} and not msg.errors and (location_ok or not still_on_edit):
            return

        details = "; ".join(msg.errors) if msg.errors else html[:500]
        raise SystemExit(f"Upload failed (HTTP {status}): {details}")


def encode_multipart(fields: list[tuple[str, str]], zip_path: Path) -> tuple[str, bytes]:
    boundary = f"----LiberTerraBoundary{uuid4().hex}"
    chunks: list[bytes] = []

    for name, value in fields:
        chunks.append(f"--{boundary}\r\n".encode())
        chunks.append(f'Content-Disposition: form-data; name="{name}"\r\n\r\n'.encode())
        chunks.append(value.encode("utf-8"))
        chunks.append(b"\r\n")

    filename = zip_path.name
    file_bytes = zip_path.read_bytes()
    chunks.append(f"--{boundary}\r\n".encode())
    chunks.append(
        (
            f'Content-Disposition: form-data; name="newfile"; filename="{filename}"\r\n'
            "Content-Type: application/zip\r\n\r\n"
        ).encode()
    )
    chunks.append(file_bytes)
    chunks.append(b"\r\n")
    chunks.append(f"--{boundary}--\r\n".encode())
    return f"multipart/form-data; boundary={boundary}", b"".join(chunks)


def changelog_to_html(text: str) -> str:
    text = text.strip()
    if not text:
        raise SystemExit("Changelog is empty")
    if "<" in text and ">" in text:
        return text
    paragraphs = [p.strip() for p in re.split(r"\n\s*\n", text) if p.strip()]
    parts: list[str] = []
    for para in paragraphs:
        lines = [ln.strip() for ln in para.splitlines() if ln.strip()]
        if lines and all(ln.startswith(("- ", "* ")) for ln in lines):
            items = "".join(f"<li>{ln[2:].strip()}</li>" for ln in lines)
            parts.append(f"<ul>{items}</ul>")
        else:
            parts.append("<p>" + "<br>".join(lines) + "</p>")
    return "".join(parts)


def resolve_game_versions(
    *,
    available: list[str],
    game_dependency: str,
    explicit: list[str] | None,
    previous_tags: list[str] | None,
    include_prerelease: bool,
) -> list[str]:
    if explicit:
        missing = [v for v in explicit if v not in available]
        if missing:
            raise SystemExit(f"Unknown game versions on ModDB: {', '.join(missing)}")
        return explicit

    # Prefer carrying forward the previous release's tags when they still exist.
    if previous_tags:
        kept = [v for v in previous_tags if v in available]
        if kept:
            return kept

    dep = parse_semver(game_dependency)
    if dep is None:
        raise SystemExit(f"Malformed game dependency: {game_dependency}")
    major, minor, *_ = dep

    selected: list[str] = []
    for name in available:
        parsed = parse_semver(name)
        if parsed is None:
            continue
        if parsed[0] != major or parsed[1] != minor:
            continue
        if not include_prerelease and parsed[3]:
            continue
        if version_ge(name, game_dependency):
            selected.append(name)
    if not selected:
        raise SystemExit(
            f"No ModDB game versions matched {major}.{minor}.* >= {game_dependency}"
        )
    return selected


def already_published(mod: dict, version: str) -> bool:
    for release in mod.get("releases") or []:
        if release.get("modversion") == version:
            return True
    return False


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--mod-id", default=DEFAULT_MOD_ID, help="ModDB url alias or numeric id (default: liberterra)")
    p.add_argument("--file", type=Path, help="Zip to upload (default: dist/LiberTerra-<version>.zip)")
    p.add_argument("--changelog", help="Changelog text (markdown-ish or HTML)")
    p.add_argument("--changelog-file", type=Path, help="Read changelog from file")
    p.add_argument(
        "--game-versions",
        nargs="+",
        help="Compatible game versions (default: previous release tags, else >= game dep)",
    )
    p.add_argument(
        "--include-prerelease",
        action="store_true",
        help="When auto-selecting versions, include pre/rc/dev tags",
    )
    p.add_argument("--session", help="vs_websessionkey cookie (default: VSMODDB_SESSION)")
    p.add_argument("--dry-run", action="store_true", help="Validate inputs without uploading")
    p.add_argument("--force", action="store_true", help="Upload even if this version already exists on ModDB")
    return p


def main() -> None:
    load_dotenv()
    args = build_parser().parse_args()
    modinfo = read_modinfo()

    zip_path = args.file or default_package_path(modinfo.version)
    if not zip_path.is_file():
        raise SystemExit(f"Package not found: {zip_path}\nRun `make package` first.")

    if args.changelog_file and args.changelog:
        raise SystemExit("Use only one of --changelog / --changelog-file")
    if args.changelog_file:
        changelog_src = args.changelog_file.read_text(encoding="utf-8")
    elif args.changelog:
        changelog_src = args.changelog
    else:
        raise SystemExit("Provide --changelog or --changelog-file")
    changelog_html = changelog_to_html(changelog_src)

    session = args.session or os.environ.get("VSMODDB_SESSION")
    if not session and not args.dry_run:
        raise SystemExit(
            "Missing session cookie. Set VSMODDB_SESSION or pass --session.\n"
            "Copy vs_websessionkey from https://mods.vintagestory.at after logging in."
        )

    # Public GETs work without auth; use a client even for dry-run version checks.
    client = ModDbClient(session or "dry-run")
    mod = client.get_mod(args.mod_id)
    numeric_mod_id = int(mod["modid"])
    latest = (mod.get("releases") or [None])[0]
    previous_tags = list(latest.get("tags") or []) if latest else []

    if already_published(mod, modinfo.version) and not args.force:
        raise SystemExit(
            f"Version {modinfo.version} is already on ModDB for {mod['name']}. "
            "Bump mod/modinfo.json or pass --force."
        )

    available = client.list_game_versions()
    game_versions = resolve_game_versions(
        available=available,
        game_dependency=modinfo.game_dependency,
        explicit=args.game_versions,
        previous_tags=previous_tags,
        include_prerelease=args.include_prerelease,
    )

    print(f"Mod:        {mod['name']} (#{numeric_mod_id}, {args.mod_id})")
    print(f"Version:    {modinfo.version}  (from {MODINFO_PATH.relative_to(ROOT)})")
    print(f"Package:    {zip_path} ({zip_path.stat().st_size} bytes)")
    print(f"Game deps:  {', '.join(game_versions)}")
    print(f"Changelog:  {len(changelog_html)} chars html")

    if args.dry_run:
        print("Dry run only — not uploading.")
        return

    assert session  # guarded above
    client = ModDbClient(session)
    page = client.fetch_edit_page(numeric_mod_id)
    if page.hovering_file_ids:
        print(f"Clearing {len(page.hovering_file_ids)} leftover hovering file(s)…")
        for file_id in page.hovering_file_ids:
            client.delete_file(file_id, page.action_token)
        page = client.fetch_edit_page(numeric_mod_id)

    print("Uploading…")
    client.upload_release(
        numeric_mod_id=numeric_mod_id,
        action_token=page.action_token,
        zip_path=zip_path,
        changelog_html=changelog_html,
        game_versions=game_versions,
    )

    # Confirm via public API
    refreshed = client.get_mod(args.mod_id)
    if not already_published(refreshed, modinfo.version):
        raise SystemExit(
            f"Upload POST completed but version {modinfo.version} is not visible on ModDB yet. "
            "Check https://mods.vintagestory.at/liberterra manually."
        )

    url = f"{MODDB_BASE}/{mod.get('urlalias') or args.mod_id}"
    print(f"Published {modinfo.version}: {url}")


if __name__ == "__main__":
    main()
