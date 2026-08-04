using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace LiberTerra.Storage;

public class BlockBookPile : Block
{
    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (blockAccessor.GetBlockEntity(pos) is BlockEntityBookPile pile)
        {
            return pile.GetCollisionBoxes();
        }

        return base.GetCollisionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        if (blockAccessor.GetBlockEntity(pos) is BlockEntityBookPile pile)
        {
            return pile.GetSelectionBoxes();
        }

        return base.GetSelectionBoxes(blockAccessor, pos);
    }

    public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        return GetCollisionBoxes(blockAccessor, pos);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return false;
        }

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityBookPile pile)
        {
            return pile.OnPlayerInteract(byPlayer, blockSel);
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityBookPile pile)
        {
            return pile.GetContentStacks();
        }

        return [];
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        // GetDrops supplies the stored books; base handles spawning + block removal.
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer)
    {
        return new WorldInteraction[]
        {
            new()
            {
                ActionLangCode = "liberterra:blockhelp-bookpile-add",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "sneak",
                Itemstacks = GetExampleBookStacks(world)
            },
            new()
            {
                ActionLangCode = "liberterra:blockhelp-bookpile-take",
                MouseButton = EnumMouseButton.Right
            },
            new()
            {
                ActionLangCode = "liberterra:blockhelp-bookpile-bulk",
                MouseButton = EnumMouseButton.Right,
                HotKeyCodes = ["ctrl"]
            }
        }.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }

    private static ItemStack[]? GetExampleBookStacks(IWorldAccessor world)
    {
        var item = world.GetItem(new AssetLocation("game", "lore-book-aged-darkgray"));
        return item is null ? null : [new ItemStack(item)];
    }

    /// <summary>
    /// Places a new book pile above the targeted face and deposits the held book(s).
    /// </summary>
    public bool CreatePile(IWorldAccessor world, BlockSelection blockSel, IPlayer player)
    {
        if (!world.Claims.TryAccess(player, blockSel.Position, EnumBlockAccessFlags.BuildOrBreak))
        {
            player.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return false;
        }

        var pos = blockSel.Position.Copy();
        if (blockSel.Face != null)
        {
            pos = pos.AddCopy(blockSel.Face);
        }

        if (pos.Y >= world.BlockAccessor.MapSizeY)
        {
            return false;
        }

        var below = world.BlockAccessor.GetBlock(pos.DownCopy());
        if (!below.CanAttachBlockAt(world.BlockAccessor, this, pos.DownCopy(), BlockFacing.UP))
        {
            return false;
        }

        if (world.BlockAccessor.GetBlock(pos).Replaceable < 6000)
        {
            return false;
        }

        world.BlockAccessor.SetBlock(BlockId, pos);

        var be = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityBookPile;
        if (be is null)
        {
            world.BlockAccessor.SetBlock(0, pos);
            return false;
        }

        if (world.Side == EnumAppSide.Client)
        {
            be.MarkClientsideFirstPlacement();
        }

        be.SetLayoutMode(BookPileUtil.GetHeldLayoutMode(player.InventoryManager.ActiveHotbarSlot.Itemstack));

        // Aim selection at the new pile for the put call.
        var pileSel = blockSel.Clone();
        pileSel.Position = pos;
        pileSel.Face = BlockFacing.UP;

        if (!be.TryPut(player))
        {
            world.BlockAccessor.SetBlock(0, pos);
            return false;
        }

        be.RegenCollision();
        world.BlockAccessor.MarkBlockDirty(pos);
        world.BlockAccessor.TriggerNeighbourBlockUpdate(pos);

        var boxes = GetCollisionBoxes(world.BlockAccessor, pos);
        if (boxes.Length > 0
            && CollisionTester.AabbIntersect(
                boxes[0],
                pos.X,
                pos.Y,
                pos.Z,
                player.Entity.SelectionBox,
                player.Entity.Pos.XYZ))
        {
            player.Entity.Pos.Y += boxes[0].Y2;
        }

        (player as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        return true;
    }
}
