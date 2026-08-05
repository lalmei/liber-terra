using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace LiberTerra.Items;

/// <summary>
/// Helpers for held lore-book stacks (max 3 distinct books, identity preserved).
/// </summary>
public static class BookStackUtil
{
    public const int MaxBooks = 3;
    public const string ContentsAttr = "contents";
    public static readonly AssetLocation StackItemCode = new("liberterra", "bookstack");

    /// <summary>Both book families stack: found lore books and the player-written ones alike.</summary>
    public static bool IsLoreBook(ItemStack? stack) => BookCodes.IsBook(stack);

    public static bool IsBookStack(ItemStack? stack)
    {
        return stack?.Item is ItemBookStack
               || (stack?.Collectible?.Code is not null
                   && stack.Collectible.Code.Equals(StackItemCode));
    }

    public static bool IsStackableBookOrStack(ItemStack? stack)
    {
        return IsLoreBook(stack) || IsBookStack(stack);
    }

    public static List<ItemStack> GetBooks(IWorldAccessor world, ItemStack stack)
    {
        var books = new List<ItemStack>();
        if (stack is null)
        {
            return books;
        }

        if (IsLoreBook(stack))
        {
            books.Add(stack.Clone());
            return books;
        }

        if (!IsBookStack(stack))
        {
            return books;
        }

        var tree = stack.Attributes.GetTreeAttribute(ContentsAttr);
        if (tree is null)
        {
            return books;
        }

        for (var i = 0; i < MaxBooks; i++)
        {
            var book = tree.GetItemstack(i.ToString());
            if (book is null)
            {
                continue;
            }

            book.ResolveBlockOrItem(world);
            if (book.Collectible is not null)
            {
                books.Add(book.Clone());
            }
        }

        return books;
    }

    public static void SetBooks(ItemStack stackItem, IList<ItemStack> books)
    {
        var tree = new TreeAttribute();
        for (var i = 0; i < books.Count && i < MaxBooks; i++)
        {
            tree[i.ToString()] = new ItemstackAttribute(books[i].Clone());
        }

        stackItem.Attributes[ContentsAttr] = tree;
        stackItem.Attributes.SetInt("bookCount", Math.Min(books.Count, MaxBooks));
    }

    public static ItemStack? CreateStackItem(ICoreAPI api, IList<ItemStack> books)
    {
        if (books.Count < 2)
        {
            return books.Count == 1 ? books[0].Clone() : null;
        }

        if (books.Count > MaxBooks)
        {
            throw new ArgumentException($"Book stack capacity is {MaxBooks}.");
        }

        var item = api.World.GetItem(StackItemCode)
                   ?? throw new InvalidOperationException($"Missing item {StackItemCode}");
        var stack = new ItemStack(item);
        SetBooks(stack, books);
        return stack;
    }

    /// <summary>
    /// Merge source books into sink books up to capacity. Returns leftover books that did not fit.
    /// </summary>
    public static (List<ItemStack> Merged, List<ItemStack> Leftover) MergeBooks(
        IList<ItemStack> sinkBooks,
        IList<ItemStack> sourceBooks)
    {
        var merged = new List<ItemStack>(sinkBooks.Count + sourceBooks.Count);
        foreach (var book in sinkBooks)
        {
            merged.Add(book.Clone());
        }

        var leftover = new List<ItemStack>();
        foreach (var book in sourceBooks)
        {
            if (merged.Count >= MaxBooks)
            {
                leftover.Add(book.Clone());
            }
            else
            {
                merged.Add(book.Clone());
            }
        }

        return (merged, leftover);
    }

    public static ItemStack NormalizeHeldStack(ICoreAPI api, IList<ItemStack> books)
    {
        if (books.Count == 0)
        {
            throw new ArgumentException("Cannot normalize an empty book list.");
        }

        if (books.Count == 1)
        {
            return books[0].Clone();
        }

        return CreateStackItem(api, books)!;
    }

    public static int GetCount(ItemStack? stack)
    {
        if (stack is null)
        {
            return 0;
        }

        if (IsLoreBook(stack))
        {
            return 1;
        }

        if (!IsBookStack(stack))
        {
            return 0;
        }

        if (stack.Attributes.HasAttribute("bookCount"))
        {
            return Math.Clamp(stack.Attributes.GetInt("bookCount"), 0, MaxBooks);
        }

        var tree = stack.Attributes.GetTreeAttribute(ContentsAttr);
        return tree?.Count ?? 0;
    }

    public static string GetCoverColor(ItemStack book)
    {
        // Both families use the same colour suffix, so a player book covers a stack the same
        // as the lore book of that colour: game:item/lore/normal-brickred and friends.
        return BookCodes.CoverColor(book.Collectible?.Code?.Path) ?? "aged-darkgray";
    }

    public static string DescribeBook(IWorldAccessor world, ItemStack book)
    {
        return book.GetName();
    }
}
