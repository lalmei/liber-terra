# Liber Terra

Don't you wish you could actually *read* more books in Vintage Story? Find them in ruins, pull them out of bony soil, and feel like the world has a library larger than three soggy pamphlets and a suspiciously identical "ancient tome"?

Yeah. Same.

So: the lore is a little ambiguous about *where* and *when* we are. Liber Terra assumes this is roughly post-1300s Earth (or close enough that nobody's checking your footnotes), and fills the shelves with literature from before that cutoff — the stuff we still have usable public-domain text for.

Epics. Chronicles. Romances. Sagas. Monks writing everything down like their ink budget depended on it. If someone in the 1200s could have owned it, and Project Gutenberg still has a clean edition, it's fair game.

## What you get

- Dozens of pre-1300 CE works as readable in-game lore volumes
- Creative inventory under the Liber Terra tab — real lore books with aged/rotten covers
- Rare world loot from:
  - **Bony soil panning**
  - **Vanilla ruin chests**
  - **Better Ruins chests** (if that mod is installed)

Loot sticks to the first volume of each work so every chest doesn't turn into a warehouse of identical Beowulfs.

Requires **Vintage Story 1.22.x**.

## Install

1. Grab the latest `LiberTerra-*.zip` from [Releases](https://github.com/lalmei/liber-terra/releases), or build from source below.
2. Drop the zip in your Vintage Story `Mods` folder, or extract it to `Mods/LiberTerra`.
3. Launch the game and enable **Liber Terra** in the mod manager.

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

Needs a .NET 10 SDK and Vintage Story 1.22.x at `/Applications/Vintage Story.app` (or set `VINTAGE_STORY`).

## License

Mod code is yours to use with the project. The book text is public-domain material redistributed from Project Gutenberg — please don't imply Gutenberg endorsed your medieval book hoarding.
