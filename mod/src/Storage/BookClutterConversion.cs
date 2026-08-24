using LiberTerra.Commands;
using LiberTerra.Lore;
using Vintagestory.API.Common;

namespace LiberTerra.Storage;

/// <summary>The real pile layout and inventory size represented by one vanilla clutter prop.</summary>
internal readonly record struct BookClutterSpec(BookPileLayoutMode Layout, int BookCount);

/// <summary>
/// Maps vanilla's decorative bookpile, bookstack and bookrow shapes onto Liber Terra's inventory
/// block, then creates distinct readable contents for it.
/// </summary>
internal static class BookClutterConversion
{
    private const string BookPilePrefix = "bookshelves/bookpile";
    private const string BookStackPrefix = "bookshelves/bookstack";
    private const string BookRowPrefix = "bookrow/bookrow";

    private static readonly int[] BookPileCounts = [16, 12, 8, 7, 17];
    private static readonly BookPileLayoutMode[] BookPileLayouts =
    [
        BookPileLayoutMode.Messy,
        BookPileLayoutMode.Uneven,
        BookPileLayoutMode.Bridged,
        BookPileLayoutMode.Scattered,
        BookPileLayoutMode.Tumbled
    ];

    private static readonly int[] BookStackCounts = [16, 13, 8, 9];
    private static readonly int[] BookRowCounts = [6, 7, 6, 7, 8, 6, 9, 7, 12, 12, 9, 12, 14, 10, 3];

    // Vanilla only supplies book-shaped random lore for these two discovery pools; its other
    // random lore outcomes are papers and scrolls, which do not belong in a book pile.
    private static readonly string[] VanillaLoreCategories = ["research", "jonas"];

    private static readonly string[] VanillaLoreCovers =
    [
        "aged-orangebrown",
        "aged-orange",
        "aged-darkgreen",
        "aged-darkgray",
        "aged-cherryred",
        "aged-brickred",
        "aged-darkolive",
        "aged-darkbeige",
        "aged-olive",
        "aged-purpleorange",
        "aged-gray",
        "rotten-gray",
        "rotten-brown",
        "rotten-rust",
        "rotten-purple",
        "rotten-green"
    ];

    public static bool TryGetSpec(string? clutterType, out BookClutterSpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(clutterType))
        {
            return false;
        }

        var pileVariant = ParseVariant(clutterType, BookPilePrefix, allowAged: true);
        if (pileVariant is not null && pileVariant <= BookPileCounts.Length)
        {
            spec = new BookClutterSpec(
                BookPileLayouts[pileVariant.Value - 1],
                BookPileCounts[pileVariant.Value - 1]);
            return true;
        }

        var stackVariant = ParseVariant(clutterType, BookStackPrefix);
        if (stackVariant is not null && stackVariant <= BookStackCounts.Length)
        {
            spec = new BookClutterSpec(BookPileLayoutMode.Neat, BookStackCounts[stackVariant.Value - 1]);
            return true;
        }

        var rowVariant = ParseVariant(clutterType, BookRowPrefix);
        if (rowVariant is not null && rowVariant <= BookRowCounts.Length)
        {
            spec = new BookClutterSpec(BookPileLayoutMode.Shelved, BookRowCounts[rowVariant.Value - 1]);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates a stable mixture: roughly one vanilla random-lore book in four, then distinct Liber
    /// Terra volumes for the rest. Candidate lists are sampled without replacement, so no converted
    /// prop contains duplicate books merely because the random source repeated itself.
    /// </summary>
    public static List<ItemStack> CreateBooks(
        ICoreAPI api,
        LiberTerraCatalog catalog,
        int requestedCount,
        Random random)
    {
        // Floor props top out at 17, while a double-sided clutter shelf can expose 28 books. Keep
        // the generator shared and bounded without forcing shelf contents through floor capacity.
        var count = Math.Clamp(requestedCount, 0, BlockEntityReadableClutterBookshelf.Capacity);
        var vanillaCount = count == 0 ? 0 : Math.Max(1, count / 4);
        var libraryCount = Math.Min(count - vanillaCount, catalog.Works.Count);
        vanillaCount = Math.Min(count - libraryCount, VanillaLoreCategories.Length * VanillaLoreCovers.Length);

        var works = catalog.Works.ToList();
        Shuffle(works, random);

        var lore = (
            from category in VanillaLoreCategories
            from cover in VanillaLoreCovers
            select (category, cover)).ToList();
        Shuffle(lore, random);

        var books = new List<ItemStack>(count);
        for (var i = 0; i < libraryCount; i++)
        {
            books.Add(LiberTerraServerCommands.CreateCompleteBook(api, works[i], random));
        }

        for (var i = 0; i < vanillaCount; i++)
        {
            var (category, cover) = lore[i];
            var item = api.World.GetItem(new AssetLocation("game", $"lore-book-{cover}"))
                ?? throw new InvalidOperationException($"Missing lore book item game:lore-book-{cover}");
            var book = new ItemStack(item);
            book.Attributes.SetString("category", category);
            books.Add(book);
        }

        Shuffle(books, random);
        return books;
    }

    private static int? ParseVariant(string clutterType, string prefix, bool allowAged = false)
    {
        if (!clutterType.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var suffix = clutterType[prefix.Length..];
        var aged = false;
        if (allowAged && suffix.StartsWith("-aged", StringComparison.Ordinal))
        {
            aged = true;
            suffix = suffix[5..];
        }

        var digitCount = 0;
        while (digitCount < suffix.Length && char.IsAsciiDigit(suffix[digitCount]))
        {
            digitCount++;
        }

        if (digitCount == 0 || !int.TryParse(suffix[..digitCount], out var variant))
        {
            return null;
        }

        // Only the two vanilla evaporating aged shapes carry a suffix after the number. Rejecting
        // every other tail keeps similarly named third-party clutter out of this migration.
        var tail = suffix[digitCount..];
        if (tail.Length > 0
            && (!aged || tail != "-evaporating" || variant is not (2 or 5)))
        {
            return null;
        }

        return variant > 0 ? variant : null;
    }

    private static void Shuffle<T>(IList<T> values, Random random)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var other = random.Next(i + 1);
            (values[i], values[other]) = (values[other], values[i]);
        }
    }
}
