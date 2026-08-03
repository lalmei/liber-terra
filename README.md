# Liber Terra

Vintage Story mod that turns curated public-domain pre-1300 CE literary works into readable lore books. Source texts come from [Project Gutenberg](https://www.gutenberg.org/).

Requires **Vintage Story 1.22.x**.

## Install

1. Download the latest `LiberTerra-*.zip` from [Releases](https://github.com/lalmei/liber-terra/releases), or build from source below.
2. Place the zip in your Vintage Story `Mods` folder, or extract it to `Mods/LiberTerra`.
3. Launch the game and enable **Liber Terra** in the mod manager.

## Features

- Dozens of pre-1300 CE works as in-game lore volumes (epics, chronicles, romances, and more)
- Creative-mode stack randomizers under the Liber Terra tab
- Rare world loot from:
  - **Bony soil panning**
  - **Vanilla ruin chests**
  - **Better Ruins chests** (if that mod is installed)

Each base work contributes its first volume to loot pools so shelves stay readable without flooding.

## Commands

| Command | Description |
| --- | --- |
| `/liberterra list` | List volumes |
| `/liberterra give <code>` | Give a complete volume (e.g. `beowulf`, `songofroland-vol1`) |
| `/liberterra giveall <baseCode>` | Give every volume of a work (e.g. `songofroland`) |

## Build

```bash
make download   # cache Gutenberg UTF-8 texts
make assets     # generate lore + lang assets
make build      # compile the code mod
make package    # zip into dist/LiberTerra-<version>.zip
make deploy     # install into ~/Library/Application Support/VintagestoryData/Mods/LiberTerra
```

Requires a .NET 10 SDK and Vintage Story 1.22.x at `/Applications/Vintage Story.app` (or set `VINTAGE_STORY`).

## License

Mod code is yours to use with the project. In-game book text is public-domain material redistributed from Project Gutenberg; do not present the Gutenberg trademark as implying PG endorsement.
