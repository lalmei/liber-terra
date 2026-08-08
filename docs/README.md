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
