using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace LiberTerra.Loot;

/// <summary>
/// Lets a survival player open the "Random Library Book" crate by hand, so old finds still become
/// books.
///
/// <see cref="LiberTerraLootTables"/> keeps the crate out of loot that has yet to be rolled, but it
/// can only reach loot tables. It cannot reach a chest in a chunk that was generated before the fix,
/// nor a crate already sitting in someone's backpack: those slots were resolved once, the answer was
/// the crate, and vanilla never resolves a slot twice.
///
/// Vanilla's only answer to a randomizer in hand is the creative stack-randomizer dialog —
/// ItemStackRandomizer.OnHeldInteractStart pushes "OpenStackRandomizerDialog" for any player, and
/// nothing in a survival client listens for it. So the crate just sits there.
///
/// Here a survival right-click finishes the job the chest left undone: Resolve the held slot in
/// place, on the server, which is the same call BlockEntityContainer would have made. Creative keeps
/// the dialog, which is what is actually useful there.
/// </summary>
public class ItemLiberTerraStackRandomizer : ItemStackRandomizer
{
    /// <summary>Vanilla's book-in-hand sound; the trailing * picks one of the three takes.</summary>
    private static readonly AssetLocation OpenSound = new("game", "sounds/held/bookclose*");

    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        var player = (byEntity as EntityPlayer)?.Player;
        if (player is null || player.WorldData.CurrentGameMode == EnumGameMode.Creative)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        // Claim the click on both sides: the client only reports a use the item said it handled,
        // and without that report the server never gets to run this.
        handling = EnumHandHandling.PreventDefault;
        if (byEntity.World.Side != EnumAppSide.Server)
        {
            return;
        }

        Open(slot, byEntity, player);
    }

    public override void GetHeldItemInfo(
        ItemSlot inSlot,
        StringBuilder dsc,
        IWorldAccessor world,
        bool withDebugInfo)
    {
        // Creative keeps vanilla's list of outcomes; it belongs with the dialog. Survival must not
        // get it — this randomizer holds one entry per volume per cover, thousands of lines that
        // would bury the tooltip and say nothing a player can act on.
        if (IsCreative(world as IClientWorldAccessor))
        {
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
            return;
        }

        dsc.AppendLine(Lang.Get("liberterra:heldinfo-stackrandomizer-crate"));
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        var help = base.GetHeldInteractionHelp(inSlot);

        // Creative gets the dialog, not a book, so only survival is told about unpacking.
        if (IsCreative((api as ICoreClientAPI)?.World))
        {
            return help;
        }

        return
        [
            new WorldInteraction
            {
                ActionLangCode = "liberterra:heldhelp-stackrandomizer-open",
                MouseButton = EnumMouseButton.Right
            },
            .. help
        ];
    }

    /// <summary>
    /// Whether the player looking at the item is in creative, for the two client-side descriptions.
    /// </summary>
    private static bool IsCreative(IClientWorldAccessor? world) =>
        world?.Player?.WorldData.CurrentGameMode == EnumGameMode.Creative;

    /// <summary>
    /// Rolls the crate into the book it stands for, in the slot the player is holding.
    /// </summary>
    private void Open(ItemSlot slot, EntityAgent byEntity, IPlayer player)
    {
        var crate = slot.Itemstack;
        if (crate is null)
        {
            return;
        }

        var world = byEntity.World;

        // resolveImports: Resolve returns without touching the slot when this is false.
        Resolve(slot, world, true);

        if (slot.Itemstack is null)
        {
            // Resolve empties the slot before it rolls, and leaves it empty if it has no stacks to
            // roll. Hand the crate back rather than eating it.
            slot.Itemstack = crate;
            slot.MarkDirty();
            world.Logger.Warning(
                "Liber Terra: {0} had nothing to resolve into, {1} keeps the crate.",
                Code,
                player.PlayerName);
            return;
        }

        slot.MarkDirty();
        world.PlaySoundAt(OpenSound, byEntity, null, true, 16f);
        world.Logger.Audit(
            "{0} opened {1} into {2}.",
            player.PlayerName,
            Code,
            slot.Itemstack.Collectible.Code);
    }
}
