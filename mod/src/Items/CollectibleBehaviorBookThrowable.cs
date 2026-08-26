using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace LiberTerra.Items;

/// <summary>
/// Hold RMB to throw a read-only book like a stone; release before windup to open/read.
/// The windup is longer than vanilla's 0.35s stone charge so a normal click still reads.
/// Unsigned writable books stay out of this behavior so ItemBook can open its editor.
/// Sneak is left free for <see cref="Storage.CollectibleBehaviorBookPileable"/>.
/// </summary>
public class CollectibleBehaviorBookThrowable : CollectibleBehaviorThrowable
{
    public CollectibleBehaviorBookThrowable(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        WindupTimeSec = BookThrowUtil.DefaultWindupSec;
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

        if (!BookCodes.IsThrowableBook(slot.Itemstack))
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

        // Eligibility can change while this same use is open: signing an empty book makes it
        // read-only. Only finish a throw that this behavior actually armed on mouse-down.
        if (byEntity.Attributes.GetInt("aiming") != 1)
        {
            return;
        }

        if (byEntity.Attributes.GetInt("aimingCancel") == 1)
        {
            return;
        }

        // Before the stack check, not after: an emptied or swapped slot still has to end the aim,
        // or the windup pose sticks. Vanilla clears it unconditionally for the same reason.
        byEntity.Attributes.SetInt("aiming", 0);
        byEntity.StopAnimation(AimAnimation);

        if (!BookCodes.IsThrowableBook(slot.Itemstack))
        {
            return;
        }

        if (secondsUsed < WindupTimeSec)
        {
            BookThrowUtil.TryOpenBook(collObj, slot, byEntity, blockSel, entitySel);
            handling = EnumHandling.PreventSubsequent;
            return;
        }

        // Re-enter vanilla throw path (clears aiming again, then spawns projectile).
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandling handling)
    {
        if (byEntity.Attributes.GetInt("aiming") != 1)
        {
            return true;
        }

        return base.OnHeldInteractStep(
            secondsUsed,
            slot,
            byEntity,
            blockSel,
            entitySel,
            ref handling);
    }

    public override bool OnHeldInteractCancel(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        EnumItemUseCancelReason cancelReason,
        ref EnumHandling handled)
    {
        if (byEntity.Attributes.GetInt("aiming") != 1)
        {
            return true;
        }

        return base.OnHeldInteractCancel(
            secondsUsed,
            slot,
            byEntity,
            blockSel,
            entitySel,
            cancelReason,
            ref handled);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot, ref EnumHandling handling)
    {
        if (!BookCodes.IsThrowableBook(inSlot.Itemstack))
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
        if (!BookCodes.IsThrowableBook(inSlot.Itemstack))
        {
            return;
        }

        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}
