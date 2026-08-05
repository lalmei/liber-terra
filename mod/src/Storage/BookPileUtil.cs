using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;

namespace LiberTerra.Storage;

/// <summary>
/// Values are persisted in block entity and item attributes, so never renumber them.
/// </summary>
public enum BookPileLayoutMode
{
    Messy = 0,
    Neat = 1,
    Tumbled = 2,
    Shelved = 3,
    Leaning = 4
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

    /// <summary>Wraps round the enum, for the empty-handed hotkey that cycles instead of picking.</summary>
    public static BookPileLayoutMode NextLayoutMode(BookPileLayoutMode mode)
    {
        var modes = Enum.GetValues<BookPileLayoutMode>();
        var index = Array.IndexOf(modes, mode);
        return modes[(index + 1) % modes.Length];
    }

    /// <summary>Keeps stored or networked values inside the enum as modes come and go.</summary>
    public static BookPileLayoutMode ClampLayoutMode(int mode)
    {
        return Enum.IsDefined(typeof(BookPileLayoutMode), mode)
            ? (BookPileLayoutMode)mode
            : BookPileLayoutMode.Messy;
    }

    public static BookPileLayoutMode GetHeldLayoutMode(ItemStack? stack)
    {
        if (stack is null)
        {
            return BookPileLayoutMode.Messy;
        }

        return ClampLayoutMode(stack.Attributes.GetInt(LayoutAttr, (int)BookPileLayoutMode.Messy));
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

    /// <summary>A lore book lying flat, in block units — the 1.9/16 that vanilla piles stack at.</summary>
    public const float BookThickness = 1.9f / 16f;

    // A book once the ground storage transform has tipped it flat: half extents along its own
    // axes, and where its centre sits relative to the pile pivot at (0.5, 0, 0.5). The transform
    // also yaws it 35 degrees, so the box is turned by that much before a slot's own tilt applies.
    private const float HalfLength = 0.1875f;      // page height, 6/16
    private const float HalfThickness = 0.059375f; // 1.9/16
    private const float HalfDepth = 0.15f;         // page width, 4.8/16
    private const float CentreX = 0.014536f;
    private const float CentreY = 0.053125f;
    private const float CentreZ = 0.049106f;

    /// <summary>The yaw the lore book's groundStorageTransform bakes into every book mesh.</summary>
    private const float GroundTransformYaw = 35f;

    private static Matrixf SlotRotation(BookPileSlotTransform slot)
    {
        // Same order genTransformationMatrices applies, so the pose here matches what is drawn.
        return new Matrixf()
            .RotateYDeg(slot.YawDeg)
            .RotateXDeg(slot.PitchDeg)
            .RotateZDeg(slot.RollDeg);
    }

    /// <summary>
    /// How high the book in this slot reaches. Exact for any pose, which matters once a layout
    /// stands books upright or props them at an angle — assuming a flat book here would leave the
    /// top of an upright row poking out of its own selection box.
    /// </summary>
    public static float SlotTopHeight(BookPileSlotTransform slot)
    {
        // Column-major 4x4: what each local axis contributes to world Y sits at 1, 5 and 9.
        // Two matrices rather than one, because Matrixf rotates in place and the centre needs
        // the slot pose alone while the extents need the book's own box, yawed by the transform.
        var pose = SlotRotation(slot).Values;
        var box = SlotRotation(slot).RotateYDeg(GroundTransformYaw).Values;

        var centreHeight = pose[1] * CentreX + pose[5] * CentreY + pose[9] * CentreZ;
        var reach = Math.Abs(box[1]) * HalfLength
                    + Math.Abs(box[5]) * HalfThickness
                    + Math.Abs(box[9]) * HalfDepth;

        return slot.Y + centreHeight + reach;
    }

    /// <summary>
    /// Hugs the tallest occupied slot so the crowning book stays clickable whatever the layout does.
    /// </summary>
    public static Cuboidf CollisionForCount(BookPileSlotTransform[] layout, int bookCount)
    {
        var count = Math.Clamp(bookCount, 1, Capacity);
        var top = BookThickness;
        for (var i = 0; i < count && i < layout.Length; i++)
        {
            top = Math.Max(top, SlotTopHeight(layout[i]));
        }

        return new Cuboidf(0.05f, 0, 0.05f, 0.95f, Math.Clamp(top + 0.04f, 0.125f, 1f), 0.95f);
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

    [JsonProperty("tumbled")]
    public BookPileSlotTransform[] Tumbled { get; set; } = [];

    [JsonProperty("shelved")]
    public BookPileSlotTransform[] Shelved { get; set; } = [];

    [JsonProperty("leaning")]
    public BookPileSlotTransform[] Leaning { get; set; } = [];

    /// <summary>Legacy single-list config (treated as messy).</summary>
    [JsonProperty("slots")]
    public BookPileSlotTransform[]? Slots { get; set; }

    public BookPileSlotTransform[] ForMode(BookPileLayoutMode mode)
    {
        var configured = mode switch
        {
            BookPileLayoutMode.Neat => Neat,
            BookPileLayoutMode.Tumbled => Tumbled,
            BookPileLayoutMode.Shelved => Shelved,
            BookPileLayoutMode.Leaning => Leaning,
            _ => Messy
        };

        if (configured is { Length: > 0 })
        {
            return configured;
        }

        if (mode == BookPileLayoutMode.Messy && Slots is { Length: > 0 })
        {
            return Slots;
        }

        return CreateDefault(mode);
    }

    public static BookPileLayoutConfig CreateDefault()
    {
        return new BookPileLayoutConfig
        {
            Messy = CreateDefault(BookPileLayoutMode.Messy),
            Neat = CreateDefault(BookPileLayoutMode.Neat),
            Tumbled = CreateDefault(BookPileLayoutMode.Tumbled),
            Shelved = CreateDefault(BookPileLayoutMode.Shelved),
            Leaning = CreateDefault(BookPileLayoutMode.Leaning)
        };
    }

    /// <summary>
    /// Fallbacks for when config/bookpile-layout.json is missing; the asset wins whenever it loads.
    /// Messy and Tumbled are traced off the vanilla bookpile1 and bookpile5 clutter shapes: four
    /// quadrant columns a book-thickness apart, all the mess coming from yaw spread rather than
    /// scatter. Yaws already cancel the 35 degrees the lore book's groundStorageTransform bakes in,
    /// and x/z already cancel the offset between that transform's centre and the pile pivot.
    /// </summary>
    public static BookPileSlotTransform[] CreateDefault(BookPileLayoutMode mode)
    {
        var poses = mode switch
        {
            BookPileLayoutMode.Neat => NeatPoses,
            BookPileLayoutMode.Tumbled => TumbledPoses,
            BookPileLayoutMode.Shelved => ShelvedPoses,
            BookPileLayoutMode.Leaning => LeaningPoses,
            _ => MessyPoses
        };

        var slots = new BookPileSlotTransform[BookPileUtil.Capacity];
        for (var i = 0; i < slots.Length; i++)
        {
            var (x, y, z, yaw, pitch, roll) = poses[i];
            slots[i] = new BookPileSlotTransform
            {
                X = x, Y = y, Z = z, YawDeg = yaw, PitchDeg = pitch, RollDeg = roll
            };
        }

        return slots;
    }

    /// <summary>Vanilla bookpile1 — four leaning columns plus one book crowning the front-left.</summary>
    private static readonly (float x, float y, float z, float yaw, float pitch, float roll)[] MessyPoses =
        [
            (0.31890f, 0.00625f, 0.76198f, -110.00f, 0.00f, 0.00f),
            (0.75481f, 0.00625f, 0.27251f, -125.00f, 0.00f, 0.00f),
            (0.30077f, 0.00625f, 0.29007f, -148.00f, 0.00f, 0.00f),
            (0.76276f, 0.00625f, 0.73675f, -80.00f, 0.00f, 0.00f),

            (0.32132f, 0.12500f, 0.73048f, -58.00f, 0.00f, 0.00f),
            (0.73827f, 0.12500f, 0.28382f, -148.00f, 0.00f, 0.00f),
            (0.31731f, 0.12500f, 0.27876f, -125.00f, 0.00f, 0.00f),
            (0.74856f, 0.12500f, 0.77251f, -125.00f, 0.00f, 0.00f),

            (0.31731f, 0.24375f, 0.78501f, -125.00f, 0.00f, 0.00f),
            (0.75938f, 0.24375f, 0.26188f, -102.00f, 0.00f, 0.00f),
            (0.31626f, 0.24375f, 0.20769f, -35.00f, 0.00f, 0.00f),
            (0.72575f, 0.24375f, 0.79401f, -170.00f, 0.00f, 0.00f),

            (0.32780f, 0.36250f, 0.76895f, -103.00f, 0.00f, 0.00f),
            (0.74452f, 0.36250f, 0.32757f, -148.00f, 0.00f, 0.00f),
            (0.28200f, 0.36250f, 0.29401f, -170.00f, 0.00f, 0.00f),
            (0.25187f, 0.48125f, 0.28545f, 144.00f, 0.00f, 0.00f)
        ];

    /// <summary>Four tidy four-book stacks, one per quadrant, all squared to the block axes.</summary>
    private static readonly (float x, float y, float z, float yaw, float pitch, float roll)[] NeatPoses =
        [
            (0.31626f, 0.00625f, 0.61644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.00625f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.31626f, 0.00625f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.00625f, 0.61644f, -35.00f, 0.00f, 0.00f),

            (0.31626f, 0.12500f, 0.61644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.12500f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.31626f, 0.12500f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.12500f, 0.61644f, -35.00f, 0.00f, 0.00f),

            (0.31626f, 0.24375f, 0.61644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.24375f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.31626f, 0.24375f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.24375f, 0.61644f, -35.00f, 0.00f, 0.00f),

            (0.31626f, 0.36250f, 0.61644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.36250f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.31626f, 0.36250f, 0.28644f, -35.00f, 0.00f, 0.00f),
            (0.71626f, 0.36250f, 0.61644f, -35.00f, 0.00f, 0.00f)
        ];

    /// <summary>Vanilla bookpile5 — the same skeleton as bookpile1 with the left half knocked about.</summary>
    private static readonly (float x, float y, float z, float yaw, float pitch, float roll)[] TumbledPoses =
        [
            (0.31708f, 0.00625f, 0.70248f, -114.00f, 0.00f, 0.00f),
            (0.66849f, 0.00625f, 0.23825f, 100.00f, 0.00f, 0.00f),
            (0.25916f, 0.00625f, 0.30409f, -153.00f, 0.00f, 0.00f),
            (0.76276f, 0.00625f, 0.73675f, -80.00f, 0.00f, 0.00f),

            (0.32132f, 0.12500f, 0.73048f, -58.00f, 0.00f, 0.00f),
            (0.73827f, 0.12500f, 0.28382f, -148.00f, 0.00f, 0.00f),
            (0.33217f, 0.12500f, 0.21991f, -121.00f, 0.00f, 0.00f),
            (0.74856f, 0.12500f, 0.77251f, -125.00f, 0.00f, 0.00f),

            (0.37981f, 0.24375f, 0.66001f, -125.00f, 0.00f, 0.00f),
            (0.75938f, 0.24375f, 0.26188f, -102.00f, 0.00f, 0.00f),
            (0.23146f, 0.24375f, 0.29047f, 167.50f, 0.00f, 0.00f),
            (0.74452f, 0.24375f, 0.79007f, -148.00f, 0.00f, 0.00f),

            (0.30862f, 0.36250f, 0.67061f, -146.00f, 0.00f, 0.00f),
            (0.74452f, 0.36250f, 0.32757f, -148.00f, 0.00f, 0.00f),
            (0.36398f, 0.36250f, 0.29341f, -162.00f, 0.00f, 0.00f),
            (0.24306f, 0.48125f, 0.27348f, 127.00f, 0.00f, 0.00f)
        ];

    /// <summary>Upright books in two back-to-back rows; the back row fills before the front.</summary>
    private static readonly (float x, float y, float z, float yaw, float pitch, float roll)[] ShelvedPoses =
        [
            (0.06187f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),
            (0.17187f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),
            (0.28187f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),
            (0.39187f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),

            (0.50188f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),
            (0.61188f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),
            (0.72188f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),
            (0.83188f, 0.17124f, 0.61144f, -0.00f, -35.00f, -90.00f),

            (0.16812f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),
            (0.27812f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),
            (0.38812f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),
            (0.49812f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),

            (0.60813f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),
            (0.71813f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),
            (0.82812f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f),
            (0.93812f, 0.17124f, 0.38856f, 180.00f, -35.00f, -90.00f)
        ];

    /// <summary>Two five-high stacks with books propped against their long faces.</summary>
    private static readonly (float x, float y, float z, float yaw, float pitch, float roll)[] LeaningPoses =
        [
            (0.51626f, 0.00625f, 0.25144f, -35.00f, -0.00f, 0.00f),
            (0.51626f, 0.00625f, 0.65144f, -35.00f, -0.00f, 0.00f),
            (0.51626f, 0.12500f, 0.25144f, -35.00f, -0.00f, 0.00f),
            (0.51626f, 0.12500f, 0.65144f, -35.00f, -0.00f, 0.00f),

            (0.77872f, 0.15784f, 0.15144f, -16.48f, -31.32f, -60.35f),
            (0.51626f, 0.24375f, 0.25144f, -35.00f, -0.00f, 0.00f),
            (0.51626f, 0.24375f, 0.65144f, -35.00f, -0.00f, 0.00f),
            (0.22128f, 0.15784f, 0.24856f, 163.52f, -31.32f, -60.35f),

            (0.51626f, 0.36250f, 0.25144f, -35.00f, -0.00f, 0.00f),
            (0.51626f, 0.36250f, 0.65144f, -35.00f, -0.00f, 0.00f),
            (0.77872f, 0.15784f, 0.45144f, -16.48f, -31.32f, -60.35f),
            (0.22128f, 0.15784f, 0.54856f, 163.52f, -31.32f, -60.35f),

            (0.51626f, 0.48125f, 0.25144f, -35.00f, -0.00f, 0.00f),
            (0.51626f, 0.48125f, 0.65144f, -35.00f, -0.00f, 0.00f),
            (0.77872f, 0.15784f, 0.75144f, -16.48f, -31.32f, -60.35f),
            (0.22128f, 0.15784f, 0.84856f, 163.52f, -31.32f, -60.35f)
        ];
}
