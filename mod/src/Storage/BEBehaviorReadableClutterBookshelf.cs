using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

/// <summary>
/// Retains vanilla's bookshelf transform and frame rendering, but removes the decorative book
/// elements after the companion block entity has created its real inventory.
/// </summary>
public class BEBehaviorReadableClutterBookshelf(BlockEntity blockentity)
    : BEBehaviorClutterBookshelf(blockentity)
{
    public override MeshData createMesh(IShapeTypeProps cprops, bool forLOD2 = false)
    {
        if (Blockentity is not BlockEntityReadableClutterBookshelf { BooksInitialized: true } shelf)
        {
            return base.createMesh(cprops, forLOD2);
        }

        shelf.CaptureAuthoredBookPoses(cprops);
        return base.createMesh(ReadableClutterShelfMesh.WithoutBakedBooks(cprops), forLOD2: false);
    }
}

/// <summary>
/// The lore shelf keeps vanilla's one specially marked discovery book while its other modeled
/// books become the same real inventory as an ordinary book-bearing clutter shelf.
/// </summary>
public class BEBehaviorReadableClutterBookshelfWithLore(BlockEntity blockentity)
    : BEBehaviorClutterBookshelfWithLore(blockentity)
{
    public override MeshData createMesh(IShapeTypeProps cprops, bool forLOD2 = false)
    {
        if (Blockentity is not BlockEntityReadableClutterBookshelf { BooksInitialized: true } shelf)
        {
            return base.createMesh(cprops, forLOD2);
        }

        shelf.CaptureAuthoredBookPoses(cprops);
        return base.createMesh(ReadableClutterShelfMesh.WithoutBakedBooks(cprops), forLOD2: false);
    }
}
