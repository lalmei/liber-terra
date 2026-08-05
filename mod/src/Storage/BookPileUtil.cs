using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace LiberTerra.Storage;

public enum BookPileLayoutMode
{
    Messy = 0,
    Neat = 1
}

public static class BookPileUtil
{
    public const int Capacity = 16;
    public const int TransferQuantity = 1;
    public const int BulkTransferQuantity = 4;
    public const string LayoutAttr = "bookPileLayout";
    public static readonly AssetLocation BlockCode = new("liberterra", "bookpile");
    public static readonly AssetLocation LayoutConfig = new("liberterra", "config/bookpile-layout.json");
    public static readonly AssetLocation PlaceSound = new("game", "sounds/block/planks");

    /// <summary>Anything <see cref="BookCodes"/> calls a book lies flat, so all of them pile.</summary>
    public static bool IsPileableBook(ItemStack? stack) => BookCodes.IsBook(stack);

    public static bool IsPileableBook(CollectibleObject? collectible) => BookCodes.IsBook(collectible);

    public static List<ItemStack> ExtractBooks(IWorldAccessor world, ItemStack stack)
    {
        if (!IsPileableBook(stack))
        {
            return [];
        }

        return [stack.Clone()];
    }

    public static BookPileLayoutMode GetHeldLayoutMode(ItemStack? stack)
    {
        if (stack is null)
        {
            return BookPileLayoutMode.Messy;
        }

        var mode = stack.Attributes.GetInt(LayoutAttr, (int)BookPileLayoutMode.Messy);
        return mode == (int)BookPileLayoutMode.Neat ? BookPileLayoutMode.Neat : BookPileLayoutMode.Messy;
    }

    public static void SetHeldLayoutMode(ItemStack? stack, BookPileLayoutMode mode)
    {
        stack?.Attributes.SetInt(LayoutAttr, (int)mode);
    }

    public static ItemStack? TakeBooksFromHeld(
        ICoreAPI api,
        ItemSlot heldSlot,
        int maxBooks,
        out List<ItemStack> taken)
    {
        taken = [];
        if (heldSlot.Itemstack is null || maxBooks <= 0 || !IsPileableBook(heldSlot.Itemstack))
        {
            return heldSlot.Itemstack;
        }

        taken.Add(heldSlot.Itemstack.Clone());
        return null;
    }

    public static Cuboidf CollisionForCount(int bookCount, BookPileLayoutMode mode)
    {
        var count = Math.Clamp(bookCount, 1, Capacity);
        float height;
        if (mode == BookPileLayoutMode.Neat)
        {
            var layers = (count + 1) / 2;
            height = Math.Max(0.125f, layers * 0.085f + 0.04f);
        }
        else
        {
            var layers = (count + 3) / 4;
            height = Math.Max(0.125f, layers * 0.12f + 0.04f);
        }

        return new Cuboidf(0.05f, 0, 0.05f, 0.95f, Math.Min(height, 1f), 0.95f);
    }
}

/// <summary>
/// One book pose inside a floor pile (block-local space).
/// </summary>
public sealed class BookPileSlotTransform
{
    [JsonProperty("x")]
    public float X { get; set; } = 0.5f;

    [JsonProperty("y")]
    public float Y { get; set; }

    [JsonProperty("z")]
    public float Z { get; set; } = 0.5f;

    [JsonProperty("yawDeg")]
    public float YawDeg { get; set; }

    [JsonProperty("pitchDeg")]
    public float PitchDeg { get; set; }

    [JsonProperty("rollDeg")]
    public float RollDeg { get; set; }
}

public sealed class BookPileLayoutConfig
{
    [JsonProperty("messy")]
    public BookPileSlotTransform[] Messy { get; set; } = [];

    [JsonProperty("neat")]
    public BookPileSlotTransform[] Neat { get; set; } = [];

    /// <summary>Legacy single-list config (treated as messy).</summary>
    [JsonProperty("slots")]
    public BookPileSlotTransform[]? Slots { get; set; }

    public BookPileSlotTransform[] ForMode(BookPileLayoutMode mode)
    {
        if (mode == BookPileLayoutMode.Neat && Neat is { Length: > 0 })
        {
            return Neat;
        }

        if (Messy is { Length: > 0 })
        {
            return Messy;
        }

        if (Slots is { Length: > 0 })
        {
            return Slots;
        }

        return mode == BookPileLayoutMode.Neat ? CreateDefaultNeat() : CreateDefaultMessy();
    }

    public static BookPileLayoutConfig CreateDefault()
    {
        return new BookPileLayoutConfig
        {
            Messy = CreateDefaultMessy(),
            Neat = CreateDefaultNeat()
        };
    }

    public static BookPileSlotTransform[] CreateDefaultMessy()
    {
        var slots = new BookPileSlotTransform[BookPileUtil.Capacity];
        var yaws = new[] { 12f, -18f, 35f, -8f, 22f, -28f, 5f, -40f, 15f, -12f, 30f, -22f, 8f, -33f, 18f, -5f };
        var offsets = new (float x, float z)[]
        {
            (0.32f, 0.32f), (0.68f, 0.30f), (0.30f, 0.68f), (0.70f, 0.70f),
            (0.38f, 0.36f), (0.62f, 0.38f), (0.36f, 0.62f), (0.64f, 0.64f),
            (0.34f, 0.40f), (0.66f, 0.34f), (0.40f, 0.66f), (0.60f, 0.60f),
            (0.42f, 0.42f), (0.58f, 0.44f), (0.44f, 0.58f), (0.56f, 0.56f),
        };

        for (var i = 0; i < slots.Length; i++)
        {
            var layer = i / 4;
            var (x, z) = offsets[i];
            slots[i] = new BookPileSlotTransform
            {
                X = x,
                Y = layer * 0.12f,
                Z = z,
                YawDeg = yaws[i],
                PitchDeg = (i % 3 - 1) * 2f,
                RollDeg = 0
            };
        }

        return slots;
    }

    public static BookPileSlotTransform[] CreateDefaultNeat()
    {
        // Alternate columns so both tidy stacks rise together.
        var slots = new BookPileSlotTransform[BookPileUtil.Capacity];
        for (var i = 0; i < BookPileUtil.Capacity; i++)
        {
            var column = i % 2;
            var layer = i / 2;
            slots[i] = new BookPileSlotTransform
            {
                X = column == 0 ? 0.32f : 0.68f,
                Y = layer * 0.085f,
                Z = 0.50f,
                YawDeg = 90f,
                PitchDeg = 0,
                RollDeg = 0
            };
        }

        return slots;
    }
}
