using LiberTerra.Config;
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

        // Throwing off: pass the use straight through, so right mouse opens the book on press with
        // no aim pose and no charge, exactly as it did before books could be thrown.
        if (!LiberTerraConfig.For(byEntity).EnableBookThrowing)
        {
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);

        // Vanilla only aims when the player is not sneaking, so read the flag back rather than
        // assuming the windup started.
        var armed = byEntity.Attributes.GetInt("aiming") == 1;
        BookThrowUtil.SetArmed(byEntity, armed);
        if (armed)
        {
            BookThrowUtil.MarkWindupStart(byEntity);
        }
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
        // read-only. Only finish a throw that this behavior actually armed on mouse-down — and ask
        // our own flag, not vanilla's "aiming", which the release-mouse cancel has already cleared
        // on the client by the time this runs.
        if (!BookThrowUtil.IsArmed(byEntity))
        {
            return;
        }

        // secondsUsed is ignored on purpose. Only the client's own clock can tell a tap from a
        // charge — see BookThrowUtil.WindupStartAttr — so the client decides and the server is
        // told, in BookUseServer.Resolve.
        var wantsThrow = byEntity.World.Side == EnumAppSide.Client
            && BookCodes.IsThrowableBook(slot.Itemstack)
            && BookThrowUtil.WantsThrow(byEntity);

        var cancelled = byEntity.Attributes.GetInt("aimingCancel") == 1;

        // Before the stack check, not after: an emptied or swapped slot still has to end the aim,
        // or the windup pose sticks. Vanilla clears it unconditionally for the same reason.
        BookThrowUtil.StopAiming(byEntity);

        if (cancelled)
        {
            return;
        }

        handling = EnumHandling.PreventSubsequent;

        if (byEntity.World.Side != EnumAppSide.Client
            || !BookCodes.IsThrowableBook(slot.Itemstack))
        {
            return;
        }

        BookThrowUtil.SendBookUse(byEntity, wantsThrow);

        if (!wantsThrow)
        {
            BookThrowUtil.TryOpenBook(collObj, slot, byEntity, blockSel, entitySel);
        }
    }

    public override bool OnHeldInteractStep(
        float secondsUsed,
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        ref EnumHandling handling)
    {
        if (!BookThrowUtil.IsArmed(byEntity))
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
        if (!BookThrowUtil.IsArmed(byEntity))
        {
            return true;
        }

        // Releasing RMB cancels first and stops second, so the windup stays armed for that stop.
        // Every other reason ends the use here, with no stop to clear the flag.
        if (cancelReason != EnumItemUseCancelReason.ReleasedMouse)
        {
            BookThrowUtil.SetArmed(byEntity, false);
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

        var read = new WorldInteraction
        {
            ActionLangCode = "liberterra:heldhelp-book-read",
            MouseButton = EnumMouseButton.Right
        };

        if (!LiberTerraConfig.Current.EnableBookThrowing)
        {
            return [read];
        }

        return
        [
            read,
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
