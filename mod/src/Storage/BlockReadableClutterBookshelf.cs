using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

/// <summary>Preserves vanilla clutter-shelf behavior while exposing its real stored books.</summary>
public class BlockReadableClutterBookshelf : BlockClutterBookshelf
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            return false;
        }

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityReadableClutterBookshelf shelf
            && shelf.OnPlayerInteract(byPlayer))
        {
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override ItemStack[] GetDrops(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        float dropQuantityMultiplier = 1)
    {
        return world.BlockAccessor.GetBlockEntity(pos) is BlockEntityReadableClutterBookshelf shelf
            ? shelf.GetStoredBooks()
            : base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer)
    {
        return ReadableClutterShelfHelp.For(world, selection)
            .Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }
}

/// <summary>
/// Keeps the one guaranteed vanilla discovery-book interaction, then exposes all the other actual
/// books now stored in that lore shelf.
/// </summary>
public class BlockReadableClutterBookshelfWithLore : BlockClutterBookshelfWithLore
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!world.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
        {
            return false;
        }

        // Vanilla's visibly marked discovery book remains the first thing taken from a lore shelf.
        if (base.OnBlockInteractStart(world, byPlayer, blockSel))
        {
            return true;
        }

        return world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityReadableClutterBookshelf shelf
               && shelf.OnPlayerInteract(byPlayer);
    }

    public override ItemStack[] GetDrops(
        IWorldAccessor world,
        BlockPos pos,
        IPlayer byPlayer,
        float dropQuantityMultiplier = 1)
    {
        return world.BlockAccessor.GetBlockEntity(pos) is BlockEntityReadableClutterBookshelf shelf
            ? shelf.GetStoredBooks()
            : base.GetDrops(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer)
    {
        return ReadableClutterShelfHelp.For(world, selection)
            .Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }
}

internal static class ReadableClutterShelfHelp
{
    public static WorldInteraction[] For(IWorldAccessor world, BlockSelection selection)
    {
        if (world.BlockAccessor.GetBlockEntity(selection.Position)
                is not BlockEntityReadableClutterBookshelf { BooksInitialized: true, BookCount: > 0 })
        {
            return [];
        }

        return
        [
            new WorldInteraction
            {
                ActionLangCode = "liberterra:blockhelp-clutterbookshelf-take",
                MouseButton = EnumMouseButton.Right
            },
            new WorldInteraction
            {
                ActionLangCode = "liberterra:blockhelp-clutterbookshelf-bulk",
                MouseButton = EnumMouseButton.Right,
                HotKeyCodes = ["ctrl"]
            }
        ];
    }
}
