using Vintagestory.API.Common;

namespace LiberTerra.Items;

/// <summary>
/// Allows lore books to inventory-merge into held stacks of up to <see cref="BookStackUtil.MaxBooks"/>.
/// </summary>
public sealed class CollectibleBehaviorBookStackable : CollectibleBehavior
{
    public CollectibleBehaviorBookStackable(CollectibleObject collObj) : base(collObj)
    {
    }

    public override int GetMergableQuantity(
        ItemStack sinkStack,
        ItemStack sourceStack,
        EnumMergePriority priority,
        ref EnumHandling handling)
    {
        if (priority < EnumMergePriority.DirectMerge)
        {
            return 0;
        }

        if (!BookStackUtil.IsLoreBook(sinkStack) || !BookStackUtil.IsStackableBookOrStack(sourceStack))
        {
            return 0;
        }

        var sinkCount = BookStackUtil.GetCount(sinkStack);
        var sourceCount = BookStackUtil.GetCount(sourceStack);
        if (sinkCount + sourceCount < 2)
        {
            return 0;
        }

        var room = BookStackUtil.MaxBooks - sinkCount;
        if (room <= 0)
        {
            return 0;
        }

        handling = EnumHandling.PreventSubsequent;
        // One inventory "unit" moves regardless of how many nested books fit.
        return 1;
    }

    public override void TryMergeStacks(ItemStackMergeOperation op, ref EnumHandling handling)
    {
        if (op.CurrentPriority < EnumMergePriority.DirectMerge)
        {
            return;
        }

        var sink = op.SinkSlot.Itemstack;
        var source = op.SourceSlot.Itemstack;
        if (sink is null || source is null
            || !BookStackUtil.IsLoreBook(sink)
            || !BookStackUtil.IsStackableBookOrStack(source))
        {
            return;
        }

        var sinkBooks = BookStackUtil.GetBooks(op.World, sink);
        var sourceBooks = BookStackUtil.GetBooks(op.World, source);
        if (sinkBooks.Count + sourceBooks.Count < 2)
        {
            return;
        }

        var (merged, leftover) = BookStackUtil.MergeBooks(sinkBooks, sourceBooks);
        if (merged.Count <= sinkBooks.Count)
        {
            return;
        }

        handling = EnumHandling.PreventSubsequent;
        op.MovableQuantity = 1;
        op.MovedQuantity = 1;

        op.SinkSlot.Itemstack = BookStackUtil.NormalizeHeldStack(op.World.Api, merged);

        if (leftover.Count == 0)
        {
            op.SourceSlot.Itemstack = null;
        }
        else
        {
            op.SourceSlot.Itemstack = BookStackUtil.NormalizeHeldStack(op.World.Api, leftover);
        }

        op.SinkSlot.MarkDirty();
        op.SourceSlot.MarkDirty();
    }
}
