using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

public class BlockEntityBookPile : BlockEntityDisplay
{
    private readonly InventoryGeneric inventory;
    private BookPileLayoutConfig layoutConfig = BookPileLayoutConfig.CreateDefault();
    private Cuboidf[] colBoxes = [BookPileUtil.CollisionForCount(1, BookPileLayoutMode.Messy)];
    private bool clientsideFirstPlacement;
    private BookPileLayoutMode layoutMode = BookPileLayoutMode.Messy;

    public BlockEntityBookPile()
    {
        inventory = new InventoryGeneric(BookPileUtil.Capacity, null, null, (_, inv) => new ItemSlot(inv));
        foreach (var slot in inventory)
        {
            slot.StorageType |= EnumItemStorageFlags.Backpack;
        }
    }

    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "liberterra-bookpile";
    public override string AttributeTransformCode => "groundStorageTransform";
    public override string ClassCode => "liberterrabookpile";

    public BookPileLayoutMode LayoutMode => layoutMode;

    public int BookCount
    {
        get
        {
            var count = 0;
            foreach (var slot in inventory)
            {
                if (!slot.Empty)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public Cuboidf[] GetCollisionBoxes() => colBoxes;
    public Cuboidf[] GetSelectionBoxes() => colBoxes;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        LoadLayout(api);
        RegenCollision();
    }

    private void LoadLayout(ICoreAPI api)
    {
        try
        {
            var asset = api.Assets.TryGet(BookPileUtil.LayoutConfig);
            if (asset is null)
            {
                return;
            }

            var config = asset.ToObject<BookPileLayoutConfig>();
            if (config is not null)
            {
                layoutConfig = config;
            }
        }
        catch (Exception exception)
        {
            api.Logger.Warning("Liber Terra book pile layout failed to load: {0}", exception.Message);
        }
    }

    public void SetLayoutMode(BookPileLayoutMode mode)
    {
        if (layoutMode == mode)
        {
            return;
        }

        layoutMode = mode;
        RegenCollision();
        MarkMeshesDirty();
        MarkDirty(true);
        Api?.World.BlockAccessor.MarkBlockDirty(Pos);
    }

    public void MarkClientsideFirstPlacement()
    {
        clientsideFirstPlacement = true;
    }

    public bool OnPlayerInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        var hotbar = byPlayer.InventoryManager.ActiveHotbarSlot;
        var sneaking = byPlayer.Entity.Controls.Sneak;

        bool ok;
        if (sneaking && !hotbar.Empty && BookPileUtil.IsPileableBook(hotbar.Itemstack))
        {
            ok = TryPut(byPlayer);
        }
        else if (!sneaking)
        {
            ok = TryTake(byPlayer);
        }
        else
        {
            ok = false;
        }

        if (ok)
        {
            RegenCollision();
            MarkDirty(true);
            if (inventory.Empty && !clientsideFirstPlacement)
            {
                Api.World.BlockAccessor.SetBlock(0, Pos);
                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            }
        }

        return ok;
    }

    public bool TryPut(IPlayer byPlayer)
    {
        var hotbar = byPlayer.InventoryManager.ActiveHotbarSlot;
        if (hotbar.Empty || BookCount >= BookPileUtil.Capacity)
        {
            return false;
        }

        var bulk = byPlayer.Entity.Controls.CtrlKey;
        var maxTake = bulk ? BookPileUtil.BulkTransferQuantity : BookPileUtil.TransferQuantity;
        maxTake = Math.Min(maxTake, BookPileUtil.Capacity - BookCount);

        var remaining = BookPileUtil.TakeBooksFromHeld(Api, hotbar, maxTake, out var taken);
        if (taken.Count == 0)
        {
            return false;
        }

        var placed = 0;
        foreach (var book in taken)
        {
            var empty = FirstEmptySlot();
            if (empty is null)
            {
                // Shouldn't happen given capacity check; put leftovers back.
                break;
            }

            empty.Itemstack = book.Clone();
            empty.MarkDirty();
            placed++;
        }

        if (placed < taken.Count)
        {
            // Restore the first unplaced book (pile-only: one book per held stack).
            remaining = taken[placed].Clone();
        }

        hotbar.Itemstack = remaining;
        hotbar.MarkDirty();

        Api.World.PlaySoundAt(
            BookPileUtil.PlaceSound.Clone().WithPathPrefixOnce("sounds/"),
            Pos.X + 0.5,
            Pos.InternalY,
            Pos.Z + 0.5,
            byPlayer,
            0.9f + (float)Api.World.Rand.NextDouble() * 0.2f,
            16);

        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        Api.World.Logger.Audit(
            "{0} Put {1} book(s) into Liber Terra book pile at {2}.",
            byPlayer.PlayerName,
            placed,
            Pos);

        return placed > 0;
    }

    public bool TryTake(IPlayer byPlayer)
    {
        if (BookCount == 0)
        {
            return false;
        }

        var bulk = byPlayer.Entity.Controls.CtrlKey;
        var takeCount = bulk ? BookPileUtil.BulkTransferQuantity : BookPileUtil.TransferQuantity;
        takeCount = Math.Min(takeCount, BookCount);

        var taken = new List<ItemStack>(takeCount);
        for (var i = 0; i < takeCount; i++)
        {
            var slot = LastFilledSlot();
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
            Pos.InternalY,
            Pos.Z + 0.5,
            byPlayer,
            0.9f + (float)Api.World.Rand.NextDouble() * 0.2f,
            16);

        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        Api.World.Logger.Audit(
            "{0} Took {1} book(s) from Liber Terra book pile at {2}.",
            byPlayer.PlayerName,
            taken.Count,
            Pos);

        return true;
    }

    private ItemSlot? FirstEmptySlot()
    {
        foreach (var slot in inventory)
        {
            if (slot.Empty)
            {
                return slot;
            }
        }

        return null;
    }

    private ItemSlot? LastFilledSlot()
    {
        for (var i = inventory.Count - 1; i >= 0; i--)
        {
            if (!inventory[i].Empty)
            {
                return inventory[i];
            }
        }

        return null;
    }

    public void RegenCollision()
    {
        colBoxes = [BookPileUtil.CollisionForCount(Math.Max(1, BookCount), layoutMode)];
    }

    protected override float[][] genTransformationMatrices()
    {
        var layout = layoutConfig.ForMode(layoutMode);
        var matrices = new float[BookPileUtil.Capacity][];
        for (var i = 0; i < BookPileUtil.Capacity; i++)
        {
            var pose = i < layout.Length ? layout[i] : new BookPileSlotTransform { X = 0.5f, Y = i * 0.05f, Z = 0.5f };
            matrices[i] = new Matrixf()
                .Translate(pose.X, pose.Y, pose.Z)
                .RotateYDeg(pose.YawDeg)
                .RotateXDeg(pose.PitchDeg)
                .RotateZDeg(pose.RollDeg)
                .Translate(-0.5f, 0, -0.5f)
                .Values;
        }

        return matrices;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetInt("layoutMode", (int)layoutMode);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        clientsideFirstPlacement = false;
        var mode = tree.GetInt("layoutMode", (int)BookPileLayoutMode.Messy);
        layoutMode = mode == (int)BookPileLayoutMode.Neat ? BookPileLayoutMode.Neat : BookPileLayoutMode.Messy;
        RegenCollision();
        RedrawAfterReceivingTreeAttributes(worldForResolving);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        dsc.AppendLine(Lang.Get("liberterra:blockinfo-bookpile-count", BookCount, BookPileUtil.Capacity));
        dsc.AppendLine(Lang.Get(
            layoutMode == BookPileLayoutMode.Neat
                ? "liberterra:blockinfo-bookpile-layout-neat"
                : "liberterra:blockinfo-bookpile-layout-messy"));

        var shown = 0;
        for (var i = inventory.Count - 1; i >= 0 && shown < 5; i--)
        {
            if (inventory[i].Empty)
            {
                continue;
            }

            dsc.AppendLine("• " + inventory[i].Itemstack!.GetName());
            shown++;
        }

        if (BookCount > shown)
        {
            dsc.AppendLine(Lang.Get("liberterra:blockinfo-bookpile-more", BookCount - shown));
        }
    }

    public ItemStack[] GetContentStacks()
    {
        var stacks = new List<ItemStack>(BookCount);
        foreach (var slot in inventory)
        {
            if (!slot.Empty)
            {
                stacks.Add(slot.Itemstack!.Clone());
            }
        }

        return stacks.ToArray();
    }
}
