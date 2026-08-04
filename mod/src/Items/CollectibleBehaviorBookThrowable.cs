using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace LiberTerra.Items;

/// <summary>
/// Hold RMB to throw a lore book like a stone; release before windup to open/read.
/// Sneak is left free for <see cref="Storage.CollectibleBehaviorBookPileable"/>.
/// </summary>
public class CollectibleBehaviorBookThrowable : CollectibleBehaviorThrowable
{
    public CollectibleBehaviorBookThrowable(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handHandling,
        ref EnumHandling handling)
    {
        if (BookThrowUtil.IsForceOpen(byEntity))
        {
            return;
        }

        if (!IsThrowableBook(slot.Itemstack))
        {
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
    }

    public override void OnHeldInteractStop(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandling handling)
    {
        if (BookThrowUtil.IsForceOpen(byEntity))
        {
            return;
        }

        if (!IsThrowableBook(slot.Itemstack))
        {
            return;
        }

        if (byEntity.Attributes.GetInt("aimingCancel") == 1)
        {
            return;
        }

        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation(AimAnimation);

        if (secondsUsed < WindupTimeSec)
        {
            BookThrowUtil.TryOpenBook(collObj, slot, byEntity, blockSel, entitySel);
            handling = EnumHandling.PreventSubsequent;
            return;
        }

        // Re-enter vanilla throw path (clears aiming again, then spawns projectile).
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        if (!IsThrowableBook(inSlot.Itemstack))
        {
            handling = EnumHandling.PassThrough;
            return [];
        }

        handling = EnumHandling.PassThrough;
        return
        [
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-book-read",
                MouseButton = EnumMouseButton.Right
            },
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-book-throw",
                MouseButton = EnumMouseButton.Right
            }
        ];
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, System.Text.StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (!IsThrowableBook(inSlot.Itemstack))
        {
            return;
        }

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }

    private static bool IsThrowableBook(ItemStack? stack)
    {
        var path = stack?.Collectible?.Code?.Path;
        return path is not null && path.StartsWith("lore-book-", StringComparison.Ordinal);
    }
}
