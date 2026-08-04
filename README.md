# Liber Terra

Don't you wish you could actually _read_ more books in Vintage Story? Find them in ruins, pull them out of bony soil, and feel like the world has a library larger the current lore?

Yeah. Same.

So: the lore is a little ambiguous about _where_ and _when_ we are. Liber Terra assumes this is roughly post-1300s Earth, or at least when it last matched with our own history, so we fill the shelves with literature from before that cutoff — the stuff we still have usable public-domain text for.

Epics. Chronicles. Romances. Sagas. If someone in the 1200s could have owned it, and Project Gutenberg still has a clean edition, it's fair game.

There are few formatting issues I noticed in some book such as the lines being longer than book allow in vintage story. But for alpha is good enough.

Note this will make it hard to get actual lore from the game, since we are dilluating loot tables with plenty of books.

However now you can try to collect all volumes of Beoweulf!

## What you get

- Dozens of pre-1300 CE works as readable in-game lore volumes
- Creative inventory under the Liber Terra tab — real lore books with aged/rotten covers
- One **Liber Terra: Random Library Book** stackrandomizer that rolls any volume and cover color
- Rare world loot from:
  - **Bony soil panning**
  - **Vanilla ruin chests**
  - **Better Ruins chests** (if that mod is installed)
- Lore books are **throwable** like stones (hold use); tap use still opens them to read

Those loot sources can roll the collection randomizer, so any volume can show up with a random aged/rotten cover.

Requires **Vintage Story 1.22.x**.

## Throwing books

Tap use to read a lore book. Hold use (about as long as throwing a stone), then release, to throw it. Thrown books always drop on impact so volumes are not destroyed.

## Roadmap

- **Book piles** — drop your haul on the floor like a civilized monk with a storage problem. Stack lore books into piles so the library lives in the world, not only in chests and backpacks.

other ideas and contributors welcome.

## Install

1. Grab the latest `LiberTerra-*.zip` from [Releases](https://github.com/lalmei/liber-terra/releases), or build from source below.
2. Drop the zip in your Vintage Story `Mods` folder, or extract it to `Mods/LiberTerra`.
3. Launch the game and enable **Liber Terra** in the mod manager.

## Commands

| Command                          | Description                                                  |
| -------------------------------- | ------------------------------------------------------------ |
| `/liberterra list`               | List volumes                                                 |
| `/liberterra give <code>`        | Give a complete volume (e.g. `beowulf`, `songofroland-vol1`) |
| `/liberterra giveall <baseCode>` | Give every volume of a work (e.g. `songofroland`)            |

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
