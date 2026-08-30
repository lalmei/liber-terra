using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

/// <summary>
/// Adds a persistent, visibly rendered book inventory to vanilla clutter shelves that contain
/// modeled books. Each side has fourteen positions; double-sided shelves keep a separate set for
/// each face.
/// Worldgen schematics only call <see cref="IRotatable"/> on the block entity class. Vanilla
/// shelves used Generic, which forwards that to the clutter behavior; this entity must do the
/// same or rotated ruins spawn with shelves still facing the schematic's original direction.
/// </summary>
public sealed class BlockEntityReadableClutterBookshelf : BlockEntityDisplay, IRotatable
{
    public const int SlotsPerSide = 14;
    public const int Capacity = SlotsPerSide * 2;

    private readonly InventoryGeneric inventory =
        new(Capacity, "liberterra-clutterbookshelf-0", null, (_, inv) => new ItemSlot(inv));

    private bool booksInitialized;
    private float[][] firstSideBookPoses = [];
    private float[][] secondSideBookPoses = [];

    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "liberterra-clutterbookshelf";
    public override string AttributeTransformCode => "onshelfTransform";
    public override string ClassCode => "liberterra-clutterbookshelf";

    public bool BooksInitialized => booksInitialized;
    public int BookCount => inventory.Count(slot => !slot.Empty);

    private BEBehaviorClutterBookshelf? ShelfBehavior => GetBehavior<BEBehaviorClutterBookshelf>();

    internal void CaptureAuthoredBookPoses(IShapeTypeProps shapeProps)
    {
        if (shapeProps is not BookShelfTypeProps props)
        {
            return;
        }

        var doubleSided = props.group.DoubleSided;
        var firstType = doubleSided ? props.Type1 : props.Code;
        firstSideBookPoses = ValidatedPoses(firstType, props.ShapeResolved);
        secondSideBookPoses = doubleSided
            ? ValidatedPoses(props.Type2, props.ShapeResolved2)
            : [];
    }

    private float[][] ValidatedPoses(string? shelfType, Shape? shape)
    {
        var poses = ReadableClutterShelfPoses.Extract(shape);
        if (!ReadableClutterShelfConversion.TryGetBookCount(shelfType, out var expectedCount)
            || poses.Length == expectedCount)
        {
            return poses;
        }

        Api.Logger.Warning(
            "Liber Terra found {0} authored book poses for clutter bookshelf {1}, expected {2}; using the safe shelf grid.",
            poses.Length,
            shelfType,
            expectedCount);
        return [];
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        RefreshClientMeshes();

        if (api.Side == EnumAppSide.Server)
        {
            // Saved block-entity attributes and schematic placement have both assigned Type/Type2
            // by the time this callback runs.
            RegisterDelayedCallback(_ => EnsurePopulated(), 0);
        }
    }

    private void EnsurePopulated()
    {
        if (booksInitialized || ShelfBehavior is not { } shelf)
        {
            return;
        }

        var firstCount = ReadableClutterShelfConversion.TryGetBookCount(shelf.Type, out var count)
            ? count
            : 0;
        var secondCount = IsDoubleSided(shelf)
                          && ReadableClutterShelfConversion.TryGetBookCount(shelf.Type2, out count)
            ? count
            : 0;
        if (firstCount == 0 && secondCount == 0)
        {
            return;
        }

        var catalog = Api.ModLoader.GetModSystem<LiberTerraModSystem>().Catalog;
        if (catalog is null || catalog.Works.Count == 0)
        {
            Api.Logger.Warning(
                "Liber Terra left clutter bookshelf {0} at {1} decorative because the catalog is unavailable.",
                shelf.Type,
                Pos);
            return;
        }

        try
        {
            var seed = GameMath.MurmurHash3(Pos.X ^ Api.World.Seed ^ 0x5f3759df, Pos.InternalY, Pos.Z);
            var books = BookClutterConversion.CreateBooks(
                Api,
                catalog,
                firstCount + secondCount,
                new Random(seed));
            if (books.Count != firstCount + secondCount)
            {
                Api.Logger.Warning(
                    "Liber Terra left clutter bookshelf {0} at {1} decorative: created {2} of {3} books.",
                    shelf.Type,
                    Pos,
                    books.Count,
                    firstCount + secondCount);
                return;
            }

            var source = 0;
            for (var i = 0; i < firstCount; i++)
            {
                inventory[i].Itemstack = books[source++].Clone();
                inventory[i].MarkDirty();
            }

            for (var i = 0; i < secondCount; i++)
            {
                inventory[SlotsPerSide + i].Itemstack = books[source++].Clone();
                inventory[SlotsPerSide + i].MarkDirty();
            }

            booksInitialized = true;
            MarkMeshesDirty();
            MarkDirty(true);
            Api.World.BlockAccessor.MarkBlockDirty(Pos);
            Api.Logger.Debug(
                "Liber Terra made clutter bookshelf {0}{1} at {2} readable with {3} actual books.",
                shelf.Type,
                secondCount > 0 ? " / " + shelf.Type2 : "",
                Pos,
                books.Count);
        }
        catch (Exception exception)
        {
            Api.Logger.Error(
                "Liber Terra failed to populate clutter bookshelf {0} at {1}: {2}",
                shelf.Type,
                Pos,
                exception);
        }
    }

    public bool OnPlayerInteract(IPlayer byPlayer)
    {
        if (!booksInitialized || BookCount == 0 || ShelfBehavior is not { } shelf)
        {
            return false;
        }

        var sideStart = IsDoubleSided(shelf) && PlayerIsBehindFirstSide(byPlayer, shelf)
            ? SlotsPerSide
            : 0;
        var quantity = byPlayer.Entity.Controls.CtrlKey ? BookPileUtil.BulkTransferQuantity : 1;
        var taken = new List<ItemStack>(quantity);

        for (var transfer = 0; transfer < quantity; transfer++)
        {
            var slot = LastFilledSlot(sideStart);
            if (slot is null)
            {
                break;
            }

            taken.Add(slot.TakeOut(1)!);
            slot.MarkDirty();
        }

        if (taken.Count == 0)
        {
            return false;
        }

        foreach (var book in taken)
        {
            if (!byPlayer.InventoryManager.TryGiveItemstack(book, true))
            {
                Api.World.SpawnItemEntity(book, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }
        }

        Api.World.PlaySoundAt(
            BookPileUtil.PlaceSound.Clone().WithPathPrefixOnce("sounds/"),
            Pos.X + 0.5,
            Pos.InternalY + 0.5,
            Pos.Z + 0.5,
            byPlayer,
            0.9f + (float)Api.World.Rand.NextDouble() * 0.2f,
            16);
        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        Api.World.Logger.Audit(
            "{0} Took {1} readable book(s) from clutter bookshelf at {2}.",
            byPlayer.PlayerName,
            taken.Count,
            Pos);

        MarkMeshesDirty();
        MarkDirty(true);
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
        return true;
    }

    public ItemStack[] GetStoredBooks()
    {
        return inventory
            .Where(slot => !slot.Empty)
            .Select(slot => slot.Itemstack!.Clone())
            .ToArray();
    }

    private ItemSlot? LastFilledSlot(int sideStart)
    {
        for (var i = sideStart + SlotsPerSide - 1; i >= sideStart; i--)
        {
            if (!inventory[i].Empty)
            {
                return inventory[i];
            }
        }

        return null;
    }

    private bool PlayerIsBehindFirstSide(IPlayer player, BEBehaviorClutterBookshelf shelf)
    {
        var yaw = EffectiveYaw(shelf);
        var toPlayerX = player.Entity.Pos.X - (Pos.X + 0.5);
        var toPlayerZ = player.Entity.Pos.Z - (Pos.Z + 0.5);
        var firstSideDot = toPlayerX * GameMath.Sin(yaw) + toPlayerZ * GameMath.Cos(yaw);
        return firstSideDot < 0;
    }

    private bool IsDoubleSided(BEBehaviorClutterBookshelf shelf)
    {
        return shelf.Variant is not null
               && Block is BlockClutterBookshelf clutter
               && clutter.variantGroupsByCode.TryGetValue(shelf.Variant, out var group)
               && group.DoubleSided;
    }

    private float EffectiveYaw(BEBehaviorClutterBookshelf shelf)
    {
        var groupYaw = 0f;
        if (shelf.Variant is not null
            && Block is BlockClutterBookshelf clutter
            && clutter.variantGroupsByCode.TryGetValue(shelf.Variant, out var group))
        {
            groupYaw = group.Rotation.Y * GameMath.DEG2RAD;
        }

        return shelf.rotateY + groupYaw;
    }

    protected override float[][] genTransformationMatrices()
    {
        var matrices = new float[Capacity][];
        if (ShelfBehavior is not { } shelf)
        {
            for (var i = 0; i < matrices.Length; i++)
            {
                matrices[i] = Matrixf.Create().Values;
            }

            return matrices;
        }

        var doubleSided = IsDoubleSided(shelf);
        var firstSideZ = shelf.Variant == "half" ? -0.25f : 0.25f;
        var yaw = EffectiveYaw(shelf);
        var shelfMatrix = new Matrixf()
            .Translate(0.5f + shelf.offsetX, 0.5f + shelf.offsetY, 0.5f + shelf.offsetZ)
            .RotateX(shelf.rotateX)
            .RotateY(yaw)
            .RotateZ(shelf.rotateZ)
            .Translate(-0.5f, -0.5f, -0.5f)
            .Values;

        var firstFaceMatrix = new Matrixf();
        if (shelf.Variant == "full" || doubleSided)
        {
            // Matches BlockClutterBookshelf.GetOrCreateMesh: the first half-depth shelf shape is
            // moved to the far face for full and double-sided variants.
            firstFaceMatrix.Translate(0, 0, 0.5f);
        }

        var secondFaceMatrix = new Matrixf()
            // GetOrCreateMesh rotates the second shape around block centre, then translates it.
            .Translate(0, 0, -0.5f)
            .Translate(0.5f, 0.5f, 0.5f)
            .RotateY(GameMath.PI)
            .Translate(-0.5f, -0.5f, -0.5f);

        for (var i = 0; i < Capacity; i++)
        {
            var sideTwo = i >= SlotsPerSide;
            var sideSlot = i % SlotsPerSide;
            var authoredPoses = sideTwo ? secondSideBookPoses : firstSideBookPoses;
            if (sideSlot < authoredPoses.Length)
            {
                var faceMatrix = sideTwo ? secondFaceMatrix.Values : firstFaceMatrix.Values;
                var facePose = Mat4f.Mul(Mat4f.Create(), faceMatrix, authoredPoses[sideSlot]);
                matrices[i] = Mat4f.Mul(Mat4f.Create(), shelfMatrix, facePose);
                continue;
            }

            // Only used if a future game version changes a shape unexpectedly. Existing empty
            // slots are never rendered, but this leaves a safe visual fallback for added books.
            var x = sideSlot % 7 * 2f / 16f + 0.0625f - 0.5f + 0.0625f;
            var y = sideSlot / 7 * 7.5f / 16f + 0.0625f;
            var z = sideTwo && doubleSided ? -0.25f : firstSideZ;

            matrices[i] = new Matrixf()
                .Translate(0.5f + shelf.offsetX, 0.5f + shelf.offsetY, 0.5f + shelf.offsetZ)
                .RotateX(shelf.rotateX)
                .RotateY(yaw)
                .RotateZ(shelf.rotateZ)
                .Translate(-0.5f, -0.5f, -0.5f)
                .Translate(x, y, z)
                .Translate(0.5f, 0, 0.5f)
                .RotateY(sideTwo && doubleSided ? GameMath.PI : 0)
                .Translate(-0.5f, 0, -0.5f)
                .Values;
        }

        return matrices;
    }

    public void OnTransformed(
        IWorldAccessor worldAccessor,
        ITreeAttribute tree,
        int degreeRotation,
        Dictionary<int, AssetLocation> oldBlockIdMapping,
        Dictionary<int, AssetLocation> oldItemIdMapping,
        EnumAxis? flipAxis)
    {
        foreach (var behavior in Behaviors)
        {
            if (behavior is IRotatable rotatable)
            {
                rotatable.OnTransformed(
                    worldAccessor,
                    tree,
                    degreeRotation,
                    oldBlockIdMapping,
                    oldItemIdMapping,
                    flipAxis);
            }
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBool("liberTerraShelfBooksInitialized", booksInitialized);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        booksInitialized = tree.GetBool("liberTerraShelfBooksInitialized");
        RefreshClientMeshes();
        RedrawAfterReceivingTreeAttributes(worldForResolving);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (booksInitialized)
        {
            dsc.AppendLine(Lang.Get("liberterra:blockinfo-clutterbookshelf-count", BookCount));
        }
    }

    private void RefreshClientMeshes()
    {
        if (Api?.Side != EnumAppSide.Client)
        {
            return;
        }

        ShelfBehavior?.loadMesh();
        MarkMeshesDirty();
        Api.World.BlockAccessor.MarkBlockDirty(Pos);
    }
}
