using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

/// <summary>
/// Migrates vanilla's decorative book clutter as its block entity enters a loaded server chunk.
/// This catches existing worlds, new worldgen schematics and player-placed clutter without scanning
/// every block in the chunk. Non-book clutter returns immediately and remains completely vanilla.
/// </summary>
public sealed class BlockEntityBehaviorBookClutterConverter(BlockEntity blockentity)
    : BlockEntityBehavior(blockentity)
{
    public override void Initialize(ICoreAPI api, JsonObject properties)
    {
        base.Initialize(api, properties);
        if (api.Side == EnumAppSide.Server)
        {
            // ShapeFromAttributes has already read saved schematic data by Initialize time. A
            // zero-delay callback also lets player placement finish assigning its item-stack type.
            Blockentity.RegisterDelayedCallback(_ => TryConvert(), 0);
        }
    }

    private void TryConvert()
    {
        var shape = Blockentity.GetBehavior<BEBehaviorShapeFromAttributes>();
        if (!BookClutterConversion.TryGetSpec(shape?.Type, out var spec))
        {
            return;
        }

        var catalog = Api.ModLoader.GetModSystem<LiberTerraModSystem>().Catalog;
        if (catalog is null || catalog.Works.Count == 0)
        {
            Api.Logger.Warning(
                "Liber Terra left book clutter {0} at {1} unchanged because the catalog is unavailable.",
                shape!.Type,
                Pos);
            return;
        }

        try
        {
            var seed = GameMath.MurmurHash3(Pos.X ^ Api.World.Seed, Pos.InternalY, Pos.Z);
            var books = BookClutterConversion.CreateBooks(Api, catalog, spec.BookCount, new Random(seed));
            if (books.Count != spec.BookCount)
            {
                Api.Logger.Warning(
                    "Liber Terra left book clutter {0} at {1} unchanged: created {2} of {3} books.",
                    shape!.Type,
                    Pos,
                    books.Count,
                    spec.BookCount);
                return;
            }

            var pileBlock = Api.World.GetBlock(BookPileUtil.BlockCode) as BlockBookPile;
            if (pileBlock is null)
            {
                Api.Logger.Warning(
                    "Liber Terra left book clutter {0} at {1} unchanged because {2} is missing.",
                    shape!.Type,
                    Pos,
                    BookPileUtil.BlockCode);
                return;
            }

            var sourceType = shape!.Type;
            var rotationX = shape.rotateX;
            var rotationY = shape.rotateY;
            var rotationZ = shape.rotateZ;
            var offsetX = shape.offsetX;
            var offsetY = shape.offsetY;
            var offsetZ = shape.offsetZ;

            Api.World.BlockAccessor.SetBlock(pileBlock.Id, Pos);
            if (Api.World.BlockAccessor.GetBlockEntity(Pos) is not BlockEntityBookPile pile)
            {
                Api.Logger.Error(
                    "Liber Terra replaced book clutter {0} at {1}, but its book-pile entity was not created.",
                    sourceType,
                    Pos);
                return;
            }

            pile.PopulateFromClutter(
                books,
                spec.Layout,
                rotationX,
                rotationY,
                rotationZ,
                offsetX,
                offsetY,
                offsetZ);
            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(Pos);
            Api.Logger.Debug(
                "Liber Terra converted {0} at {1} into {2} readable books ({3}).",
                sourceType,
                Pos,
                books.Count,
                spec.Layout);
        }
        catch (Exception exception)
        {
            Api.Logger.Error(
                "Liber Terra failed to convert book clutter {0} at {1}: {2}",
                shape!.Type,
                Pos,
                exception);
        }
    }
}
