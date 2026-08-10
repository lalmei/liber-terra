using LiberTerra.Items;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

/// <summary>
/// Sneak + RMB places lore books into a Liber Terra book pile (replaces Quadrants for new lore-book placement).
/// Pre-existing vanilla GroundStorage (Quadrants) blocks are left alone so old worlds keep working.
/// F while looking at a pile (or placeable ground) chooses Messy vs Neat layout.
/// </summary>
public sealed class CollectibleBehaviorBookPileable : CollectibleBehavior
{
    private SkillItem[]? layoutModes;

    public CollectibleBehaviorBookPileable(CollectibleObject collObj) : base(collObj)
    {
    }

    private const string LayoutModeCacheKey = "liberterra-bookpile-layout-modes";

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is not ICoreClientAPI capi)
        {
            return;
        }

        layoutModes = ObjectCacheUtil.GetOrCreate(capi, LayoutModeCacheKey, () =>
        {
            // Index must line up with BookPileLayoutMode, which the tool mode int maps straight onto.
            return new SkillItem[]
            {
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "messy"),
                    Name = Lang.Get("liberterra:bookpile-layout-messy")
                }.WithIcon(capi, BookPileLayoutIcons.DrawMessy),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "neat"),
                    Name = Lang.Get("liberterra:bookpile-layout-neat")
                }.WithIcon(capi, BookPileLayoutIcons.DrawNeat),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "tumbled"),
                    Name = Lang.Get("liberterra:bookpile-layout-tumbled")
                }.WithIcon(capi, BookPileLayoutIcons.DrawTumbled),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "shelved"),
                    Name = Lang.Get("liberterra:bookpile-layout-shelved")
                }.WithIcon(capi, BookPileLayoutIcons.DrawShelved),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "leaning"),
                    Name = Lang.Get("liberterra:bookpile-layout-leaning")
                }.WithIcon(capi, BookPileLayoutIcons.DrawLeaning),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "uneven"),
                    Name = Lang.Get("liberterra:bookpile-layout-uneven")
                }.WithIcon(capi, BookPileLayoutIcons.DrawUneven),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "bridged"),
                    Name = Lang.Get("liberterra:bookpile-layout-bridged")
                }.WithIcon(capi, BookPileLayoutIcons.DrawBridged),
                new SkillItem
                {
                    Code = new AssetLocation("liberterra", "scattered"),
                    Name = Lang.Get("liberterra:bookpile-layout-scattered")
                }.WithIcon(capi, BookPileLayoutIcons.DrawScattered)
            };
        });
    }

    public override void OnUnloaded(ICoreAPI api)
    {
        base.OnUnloaded(api);

        if (api is not ICoreClientAPI capi || ObjectCacheUtil.TryGet<SkillItem[]>(capi, LayoutModeCacheKey) is null)
        {
            return;
        }

        // Shared across every lore book, so tear the cached textures down exactly once.
        foreach (var mode in layoutModes ?? [])
        {
            mode?.Dispose();
        }

        ObjectCacheUtil.Delete(capi, LayoutModeCacheKey);
        layoutModes = null;
    }

    public override void OnHeldInteractStart(
        ItemSlot itemslot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (!TryInteract(itemslot, byEntity, blockSel, ref handHandling))
        {
            return;
        }

        handling = EnumHandling.PreventSubsequent;
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        handling = EnumHandling.PassThrough;
        return
        [
            new WorldInteraction
            {
                HotKeyCode = "shift",
                ActionLangCode = "liberterra:heldhelp-bookpile-place",
                MouseButton = EnumMouseButton.Right
            },
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-bookpile-layout",
                HotKeyCode = "toolmodeselect",
                MouseButton = EnumMouseButton.None
            }
        ];
    }

    public override SkillItem[]? GetToolModes(ItemSlot slot, IClientPlayer forPlayer, BlockSelection blockSel)
    {
        if (blockSel is null || !BookPileUtil.CanFillPile(slot.Itemstack))
        {
            return null;
        }

        if (FindTargetPile(forPlayer.Entity.World, blockSel) is not null)
        {
            return layoutModes;
        }

        // Also allow choosing layout before the first book goes down.
        if (blockSel.Face == BlockFacing.UP)
        {
            return layoutModes;
        }

        return null;
    }

    public override int GetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection)
    {
        if (blockSelection is not null)
        {
            var pile = FindTargetPile(byPlayer.Entity.World, blockSelection);
            if (pile is not null)
            {
                return (int)pile.LayoutMode;
            }
        }

        return (int)BookPileUtil.GetHeldLayoutMode(slot.Itemstack);
    }

    public override void SetToolMode(ItemSlot slot, IPlayer byPlayer, BlockSelection blockSelection, int toolMode)
    {
        var mode = BookPileUtil.ClampLayoutMode(toolMode);

        BookPileUtil.SetHeldLayoutMode(slot.Itemstack, mode);
        slot.MarkDirty();

        if (blockSelection is null)
        {
            return;
        }

        var pile = FindTargetPile(byPlayer.Entity.World, blockSelection);
        if (pile is null)
        {
            return;
        }

        if (!byPlayer.Entity.World.Claims.TryAccess(byPlayer, pile.Pos, EnumBlockAccessFlags.BuildOrBreak))
        {
            return;
        }

        pile.SetLayoutMode(mode);
    }

    public static bool TryInteract(
        ItemSlot itemslot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        ref EnumHandHandling handHandling)
    {
        var world = byEntity?.World;

        // ShiftKey, not Sneak: they are separate controls, and ShiftKey is the one the client
        // routes a right-click by and the one vanilla's throwable checks before it starts aiming.
        // Reading the other flag lets a single click both aim and place, which leaves the throw
        // pose on a player whose hand just emptied. Ground storage gates on ShiftKey too.
        if (blockSel is null || world is null || !byEntity!.Controls.ShiftKey)
        {
            return false;
        }

        if (!BookPileUtil.CanFillPile(itemslot.Itemstack))
        {
            return false;
        }

        if (byEntity is not EntityPlayer entityPlayer)
        {
            return false;
        }

        var byPlayer = world.PlayerByUid(entityPlayer.PlayerUID);
        if (byPlayer is null)
        {
            return false;
        }

        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            itemslot.MarkDirty();
            return false;
        }

        // Interact with an existing Liber Terra pile (targeted block or the one above).
        var pile = FindTargetPile(world, blockSel);
        if (pile is not null)
        {
            if (pile.OnPlayerInteract(byPlayer, blockSel))
            {
                BookThrowUtil.StopAiming(byEntity);
                handHandling = EnumHandHandling.PreventDefault;
                return true;
            }

            return false;
        }

        // Legacy Quadrants: do not create a Liber Terra pile on/against existing groundstorage.
        // Empty-hand take/break still go through vanilla BlockGroundStorage.
        if (FindTargetGroundStorage(world, blockSel) is not null)
        {
            return false;
        }

        if (blockSel.Face != BlockFacing.UP)
        {
            return false;
        }

        var pileBlock = world.GetBlock(BookPileUtil.BlockCode) as BlockBookPile;
        if (pileBlock is null)
        {
            return false;
        }

        var onBlock = world.BlockAccessor.GetBlock(blockSel.Position);
        if (!onBlock.CanAttachBlockAt(world.BlockAccessor, pileBlock, blockSel.Position, BlockFacing.UP))
        {
            return false;
        }

        var above = blockSel.Position.AddCopy(blockSel.Face);
        if (world.BlockAccessor.GetBlock(above).Replaceable < 6000)
        {
            return false;
        }

        if (pileBlock.CreatePile(world, blockSel, byPlayer))
        {
            BookThrowUtil.StopAiming(byEntity);
            handHandling = EnumHandHandling.PreventDefault;
            return true;
        }

        return false;
    }

    public static BlockEntityBookPile? FindTargetPile(IWorldAccessor world, BlockSelection blockSel)
    {
        return world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBookPile
               ?? world.BlockAccessor.GetBlockEntity(blockSel.Position.UpCopy()) as BlockEntityBookPile;
    }

    /// <summary>
    /// Pre-mod Quadrants piles live as vanilla <see cref="BlockEntityGroundStorage"/>.
    /// Mirror vanilla's lookup so we never stack a Liber Terra pile onto them.
    /// </summary>
    public static BlockEntityGroundStorage? FindTargetGroundStorage(IWorldAccessor world, BlockSelection blockSel)
    {
        return world.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityGroundStorage
               ?? world.BlockAccessor.GetBlockEntity(blockSel.Position.UpCopy()) as BlockEntityGroundStorage;
    }
}
