using Vintagestory.API.Common;

namespace LiberTerra;

/// <summary>
/// Which item codes count as a book, in one place, so piling, stacking and throwing can never
/// disagree about it.
///
/// Vanilla ships two book families and they share a shape (block/clutter/bookshelves/small-normal),
/// a cover texture path and a groundStorageTransform: <c>lore-book-*</c> (found, read-only) and
/// <c>book-*</c>, of which <c>book-normal-*</c> are the ones players write and sign.
///
/// Scrolls, paper, letters and envelopes are not books. They are bookshelveable, and they carry our
/// behaviors too — the JSON patches land on a whole itemtype file and lore.json holds all of them —
/// but their groundStorageTransform has no z:90, so they never lie flat and would stand on end
/// wherever a book is meant to rest. That is why membership is decided by code and not by behavior.
///
/// The domain is deliberately not checked. Book mods build on the same two families, so Bookbinders'
/// <c>bookbinders:book-normal-white</c> piles, stacks and throws like the vanilla book it copies
/// without Liber Terra having to name it.
/// </summary>
public static class BookCodes
{
    private const string FoundPrefix = "lore-book-";
    private const string WritablePrefix = "book-";

    public static bool IsBook(string? codePath) => CoverColor(codePath) is not null;

    public static bool IsBook(CollectibleObject? collectible) => IsBook(collectible?.Code?.Path);

    public static bool IsBook(ItemStack? stack) => IsBook(stack?.Item?.Code?.Path);

    /// <summary>
    /// The colour suffix both families share, e.g. "normal-brickred". For vanilla it doubles as the
    /// <c>game:item/lore/{colour}</c> texture name, so a player book covers a stack exactly as a
    /// lore book of the same colour does; a modded book names its own colours, so
    /// <see cref="Items.BookStackUtil.GetCoverTexture"/> reads the cover off the item and keeps this
    /// only as a fallback. Null when the code is not a book.
    /// </summary>
    public static string? CoverColor(string? codePath)
    {
        if (codePath is null)
        {
            return null;
        }

        if (codePath.StartsWith(FoundPrefix, StringComparison.Ordinal))
        {
            return codePath[FoundPrefix.Length..];
        }

        return codePath.StartsWith(WritablePrefix, StringComparison.Ordinal)
            ? codePath[WritablePrefix.Length..]
            : null;
    }
}
