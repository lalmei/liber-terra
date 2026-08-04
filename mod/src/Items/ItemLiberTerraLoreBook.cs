using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace LiberTerra.Items;

/// <summary>
/// Lore item that prefers collectible behaviors (e.g. book piles) before vanilla open-on-RMB.
/// </summary>
public class ItemLiberTerraLoreBook : ItemRandomLore
{
    public override void OnHeldInteractStart(
        ItemSlot slot,
        EntityAgent byEntity,
        BlockSelection blockSel,
        EntitySelection entitySel,
        bool firstEvent,
        ref EnumHandHandling handling)
    {
        // Mirror ItemStone: run behaviors first so BookPileable can claim the use.
        var behaviorHandling = EnumHandHandling.NotHandled;
        foreach (var behavior in CollectibleBehaviors)
        {
            var handled = EnumHandling.PassThrough;
            behavior.OnHeldInteractStart(
                slot,
                byEntity,
                blockSel,
                entitySel,
                firstEvent,
                ref behaviorHandling,
                ref handled);

            if (handled != EnumHandling.PassThrough)
            {
                handling = behaviorHandling;
            }

            if (handled == EnumHandling.PreventSubsequent)
            {
                return;
            }
        }

        if (behaviorHandling != EnumHandHandling.NotHandled)
        {
            handling = behaviorHandling;
            return;
        }

        base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
    }
}
