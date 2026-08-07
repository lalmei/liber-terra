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
- [Command Reference](player/commands.md): `/liberterra` commands.
