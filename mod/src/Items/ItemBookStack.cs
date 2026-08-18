using System.Text;
using LiberTerra.Storage;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LiberTerra.Items;

/// <summary>
/// Held stack of 2–3 lore books. Tracks each book for unstacking / one-by-one drops.
/// </summary>
public class ItemBookStack : Item
{
    private const string MeshCacheKey = "liberterra-bookstack-meshes";
    private const string AimAnimation = BookThrowUtil.AimAnimation;

    public override void OnBeforeRender(
        ICoreClientAPI capi,
        ItemStack itemstack,
        EnumItemRenderTarget target,
        ref ItemRenderInfo renderinfo)
    {
        var cache = ObjectCacheUtil.GetOrCreate(
            capi,
            MeshCacheKey,
            () => new Dictionary<string, MultiTextureMeshRef>());

        var key = GetMeshKey(capi.World, itemstack);
        if (!cache.TryGetValue(key, out var meshRef) || meshRef is null || meshRef.Disposed)
        {
            meshRef = CreateMeshRef(capi, itemstack);
            cache[key] = meshRef;
        }

        renderinfo.ModelRef = meshRef;
        base.OnBeforeRender(capi, itemstack, target, ref renderinfo);
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);
        if (api is not ICoreClientAPI)
        {
            return;
        }

        var cache = ObjectCacheUtil.TryGet<Dictionary<string, MultiTextureMeshRef>>(api, MeshCacheKey);
        if (cache is null)
        {
            return;
        }

        foreach (var mesh in cache.Values)
        {
            mesh?.Dispose();
        }

        ObjectCacheUtil.Delete(api, MeshCacheKey);
    }

    private static string GetMeshKey(IWorldAccessor world, ItemStack stack)
    {
        var books = BookStackUtil.GetBooks(world, stack);
        var count = Math.Clamp(books.Count, 2, BookStackUtil.MaxBooks);
        var covers = new string[count];
        for (var i = 0; i < count; i++)
        {
            // The whole texture, not just the colour suffix: another mod's book can share a
            // suffix with a vanilla one and still be a different cover, and two different
            // covers must never land on the same cached mesh.
            covers[i] = CoverTexture(books, i).ToShortString();
        }

        return count + "-" + string.Join("-", covers);
    }

    /// <summary>
    /// The cover for slot <paramref name="index"/>, or the default once the shape has more slots
    /// than the stack has books. Keyed and tesselated off the same call so a cached mesh always
    /// matches the key that found it.
    /// </summary>
    private static AssetLocation CoverTexture(List<ItemStack> books, int index)
    {
        return index < books.Count
            ? BookStackUtil.GetCoverTexture(books[index])
            : BookStackUtil.DefaultCoverTexture;
    }

    private MultiTextureMeshRef CreateMeshRef(ICoreClientAPI capi, ItemStack stack)
    {
        var books = BookStackUtil.GetBooks(capi.World, stack);
        var count = Math.Clamp(books.Count, 2, BookStackUtil.MaxBooks);
        var shapePath = new AssetLocation("liberterra", $"shapes/item/bookstack-{count}.json");
        var shape = Vintagestory.API.Common.Shape.TryGet(capi, shapePath)
                    ?? throw new InvalidOperationException($"Missing bookstack shape {shapePath}");

        var textures = new Dictionary<string, AssetLocation>(count);
        for (var i = 0; i < count; i++)
        {
            textures[$"cover{i}"] = CoverTexture(books, i);
        }

        var texSource = new ContainedTextureSource(
            capi,
            capi.ItemTextureAtlas,
            textures,
            $"Liber Terra bookstack ({Code})");

        var mesh = new MeshData();
        capi.Tesselator.TesselateShape(
            new TesselationMetaData
            {
                TexSource = texSource,
                WithJointIds = false,
                WithDamageEffect = true
            },
            shape,
            out mesh);

        return capi.Render.UploadMultiTextureMesh(mesh);
    }

    public override int GetMergableQuantity(ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
    {
        if (priority < EnumMergePriority.DirectMerge)
        {
            return 0;
        }

        if (!BookStackUtil.IsBookStack(sinkStack) || !BookStackUtil.IsStackableBookOrStack(sourceStack))
        {
            return 0;
        }

        var room = BookStackUtil.MaxBooks - BookStackUtil.GetCount(sinkStack);
        return room > 0 ? 1 : 0;
    }

    public override void TryMergeStacks(ItemStackMergeOperation op)
    {
        if (op.CurrentPriority < EnumMergePriority.DirectMerge)
        {
            op.MovableQuantity = 0;
            op.MovedQuantity = 0;
            return;
        }

        var sink = op.SinkSlot.Itemstack;
        var source = op.SourceSlot.Itemstack;
        if (sink is null || source is null
            || !BookStackUtil.IsBookStack(sink)
            || !BookStackUtil.IsStackableBookOrStack(source))
        {
            op.MovableQuantity = 0;
            op.MovedQuantity = 0;
            return;
        }

        var sinkBooks = BookStackUtil.GetBooks(op.World, sink);
        var sourceBooks = BookStackUtil.GetBooks(op.World, source);
        var (merged, leftover) = BookStackUtil.MergeBooks(sinkBooks, sourceBooks);
        if (merged.Count <= sinkBooks.Count)
        {
            op.MovableQuantity = 0;
            op.MovedQuantity = 0;
            return;
        }

        op.MovableQuantity = 1;
        op.MovedQuantity = 1;
        op.SinkSlot.Itemstack = BookStackUtil.NormalizeHeldStack(api, merged);

        if (leftover.Count == 0)
        {
            op.SourceSlot.Itemstack = null;
        }
        else
        {
            op.SourceSlot.Itemstack = BookStackUtil.NormalizeHeldStack(api, leftover);
        }

        op.SinkSlot.MarkDirty();
        op.SourceSlot.MarkDirty();
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var count = BookStackUtil.GetCount(itemStack);
        return Lang.Get("liberterra:item-bookstack-name", count);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

        if (inSlot.Itemstack is null)
        {
            return;
        }

        dsc.AppendLine();
        dsc.AppendLine(Lang.Get("liberterra:item-bookstack-contents"));
        var books = BookStackUtil.GetBooks(world, inSlot.Itemstack);
        for (var i = 0; i < books.Count; i++)
        {
            dsc.AppendLine($"  {i + 1}. {BookStackUtil.DescribeBook(world, books[i])}");
        }

        dsc.AppendLine();
        dsc.AppendLine(Lang.Get("liberterra:item-bookstack-help"));
        dsc.AppendLine(Lang.Get(
            "throwable-damage-and-damagetype-when-thrown",
            1,
            Lang.Get(EnumDamageType.BluntAttack.ToString())));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return
        [
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-bookstack-read",
                MouseButton = EnumMouseButton.Right
            },
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-bookstack-throw",
                MouseButton = EnumMouseButton.Right
            },
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-bookstack-unstack",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "shift"
            },
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-bookpile-place",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "shift"
            },
            // This override replaces the list rather than appending to it, so the pile lines the
            // BookPileable behavior contributes to a loose book have to be repeated here.
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-bookpile-layout",
                HotKeyCode = "toolmodeselect",
                MouseButton = EnumMouseButton.None
            }
        ];
    }

    /// <summary>
    /// Both arms are wrapped around the stack, so there is no swing to play.
    /// </summary>
    public override string? GetHeldTpHitAnimation(ItemSlot slot, Entity byEntity)
    {
        return null;
    }

    /// <summary>
    /// PreventDefault (4) is the one value the client reads as "no attack animation and
    /// no survival block breaking" — you cannot mine or hit while your arms are full.
    /// The deliberate actions still live on use: read, throw, unstack, pile.
    /// </summary>
    public override void OnHeldAttackStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandHandling handling)
    {
        handling = EnumHandHandling.PreventDefault;
    }

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (slot.Itemstack is null)
        {
            return;
        }

        var books = BookStackUtil.GetBooks(byEntity.World, slot.Itemstack);
        if (books.Count == 0)
        {
            return;
        }

        // Sneak + block: place into / create a floor book pile. ShiftKey rather than Sneak, so
        // this and the aim below stay mutually exclusive — see CollectibleBehaviorBookPileable.
        if (byEntity.Controls.ShiftKey && blockSel is not null)
        {
            if (CollectibleBehaviorBookPileable.TryInteract(slot, byEntity, blockSel, ref handling))
            {
                return;
            }
        }

        // Sneak + empty air: pop top book into inventory (unstack one).
        if (byEntity.Controls.ShiftKey)
        {
            BookThrowUtil.StopAiming(byEntity);
            handling = EnumHandHandling.PreventDefault;
            if (byEntity.World.Side != EnumAppSide.Server)
            {
                return;
            }

            PopTopIntoInventory(slot, byEntity);
            return;
        }

        // Non-sneak: aim for throw; short release opens, long release throws top book.
        byEntity.Attributes.SetInt("aiming", 1);
        byEntity.Attributes.SetInt("aimingCancel", 0);
        byEntity.StartAnimation(AimAnimation);
        handling = EnumHandHandling.PreventDefault;
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        if (byEntity.Attributes.GetInt("aimingCancel") == 1)
        {
            return false;
        }

        return true;
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason)
    {
        BookThrowUtil.StopAiming(byEntity);
        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
        {
            byEntity.Attributes.SetInt("aimingCancel", 1);
        }

        return true;
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel)
    {
        if (byEntity.Attributes.GetInt("aimingCancel") == 1)
        {
            return;
        }

        BookThrowUtil.StopAiming(byEntity);

        if (slot.Itemstack is null)
        {
            return;
        }

        var books = BookStackUtil.GetBooks(byEntity.World, slot.Itemstack);
        if (books.Count == 0)
        {
            return;
        }

        if (secondsUsed < BookThrowUtil.DefaultWindupSec)
        {
            ReadTopBook(slot, byEntity, blockSel, entitySel, books);
            return;
        }

        ThrowTopBook(slot, byEntity, books);
    }

    private void ReadTopBook(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection? blockSel,
        EntitySelection? entitySel,
        List<ItemStack> books)
    {
        var stackedItem = slot.Itemstack!;
        slot.Itemstack = books[^1].Clone();

        try
        {
            BookThrowUtil.TryOpenBook(
                slot.Itemstack.Collectible,
                slot,
                byEntity,
                blockSel,
                entitySel);

            if (slot.Itemstack is not null)
            {
                books[^1] = slot.Itemstack.Clone();
            }
        }
        finally
        {
            // ItemRandomLore needs the real hotbar slot to open its GUI. Restore the
            // composite stack after it has read or updated the selected top book.
            slot.Itemstack = stackedItem;
            BookStackUtil.SetBooks(stackedItem, books);
            slot.MarkDirty();
        }
    }

    private void ThrowTopBook(ItemSlot slot, EntityAgent byEntity, List<ItemStack> books)
    {
        var thrown = books[^1].Clone();
        books.RemoveAt(books.Count - 1);

        if (books.Count == 0)
        {
            slot.Itemstack = null;
        }
        else
        {
            slot.Itemstack = BookStackUtil.NormalizeHeldStack(api, books);
        }

        slot.MarkDirty();

        BookThrowUtil.TryThrowItemStack(
            byEntity,
            thrown,
            BookThrowUtil.DefaultBookProjectileConfig());
    }

    public override void OnHeldDropped(
        IWorldAccessor world,
        IPlayer byPlayer,
        ItemSlot slot,
        int quantity,
        ref EnumHandling handling)
    {
        if (slot.Itemstack is null || world.Side != EnumAppSide.Server)
        {
            handling = EnumHandling.PreventSubsequent;
            return;
        }

        var books = BookStackUtil.GetBooks(world, slot.Itemstack);
        if (books.Count == 0)
        {
            return;
        }

        handling = EnumHandling.PreventSubsequent;

        var dropped = books[^1].Clone();
        books.RemoveAt(books.Count - 1);

        if (books.Count == 0)
        {
            slot.Itemstack = null;
        }
        else
        {
            slot.Itemstack = BookStackUtil.NormalizeHeldStack(api, books);
        }

        slot.MarkDirty();

        var eye = byPlayer.Entity.LocalEyePos;
        var pos = byPlayer.Entity.Pos.XYZ.AddCopy(eye.X, eye.Y - 0.2, eye.Z);
        var view = byPlayer.Entity.Pos.GetViewVector();
        var velocity = new Vec3d(view.X, view.Y, view.Z).Mul(0.15);

        world.SpawnItemEntity(dropped, pos, velocity);
    }

    private void PopTopIntoInventory(ItemSlot slot, EntityAgent byEntity)
    {
        if (slot.Itemstack is null || byEntity is not EntityPlayer playerEntity)
        {
            return;
        }

        var books = BookStackUtil.GetBooks(byEntity.World, slot.Itemstack);
        if (books.Count == 0)
        {
            return;
        }

        var popped = books[^1].Clone();
        books.RemoveAt(books.Count - 1);

        if (books.Count == 0)
        {
            slot.Itemstack = null;
        }
        else if (books.Count == 1)
        {
            slot.Itemstack = books[0];
        }
        else
        {
            BookStackUtil.SetBooks(slot.Itemstack, books);
        }

        slot.MarkDirty();

        var player = byEntity.World.PlayerByUid(playerEntity.PlayerUID);
        if (player is null)
        {
            byEntity.World.SpawnItemEntity(popped, byEntity.Pos.XYZ);
            return;
        }

        if (!player.InventoryManager.TryGiveItemstack(popped, true))
        {
            byEntity.World.SpawnItemEntity(popped, byEntity.Pos.XYZ);
        }
    }
}
