# Liber Terra Docs

Player-facing documentation for the Liber Terra Vintage Story mod.

The site builds with ProperDocs and MaterialX. Dependencies live in the root
`pyproject.toml` / `uv.lock` under the `docs` group.

```bash
uv sync
make docs-serve   # or: make docs-build
```

## Player Docs

- [Player Guide](player/guide.md): what Liber Terra adds and how to use it.
- [The Library](player/library.md): full catalog of works, volumes, give codes, and sources.
- [Command Reference](player/commands.md): `/liberterra` commands.

## Modding Docs

- [Book Transforms](dev/transforms.md): how a book mesh is placed — the ground transform,
  pile slot poses, and the conventions that are easy to get backwards.

Figures under `docs/images/` are generated from the mod source by `make docs-figures`; edit the
tools rather than the SVGs.

## ModDB Description

[`moddb-description.html`](moddb-description.html) is the paste-ready HTML source for the
TinyMCE description editor on mods.vintagestory.at. Keep it synchronized with the root README
whenever the catalog, features, compatibility, requirements, or release highlights change. Paste
everything below its leading maintenance comment into TinyMCE's source view, replace screenshot
markers with images uploaded to the mod page, preview, and then save. Uploaded image URLs live in
[`moddb-images.json`](moddb-images.json); `make moddb-copy` refuses to emit a fragment when any
screenshot marker lacks a mapping.

```bash
make moddb-preview  # render with local screenshots and working spoilers, then open it
make moddb-copy     # copy comment-free HTML with ModDB CDN images to the clipboard
```
