using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace LiberTerra.Storage;

/// <summary>
/// Makes the layout hotkey work with empty hands as well as with a book.
///
/// The game's tool mode picker is driven entirely by the held item: GuiDialogToolMode takes only
/// an ICoreClientAPI and fills itself from the active hotbar slot, with no way to hand it modes
/// from outside. So with nothing in hand there is no picker to open. Rather than reimplement it,
/// empty hands step to the next layout and the new name is echoed. Holding a book still opens the
/// real picker through <see cref="CollectibleBehaviorBookPileable"/>.
/// </summary>
public sealed class BookPileLayoutHotkey : ModSystem
{
    private ICoreClientAPI? capi;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        capi = api;

        var hotkey = api.Input.GetHotKeyByCode("toolmodeselect");
        if (hotkey is null)
        {
            return;
        }

        // Chain rather than replace: anything we do not handle still reaches the tool mode picker,
        // so chisels and the rest keep working.
        var previous = hotkey.Handler;
        hotkey.Handler = combo => CycleLayout() || (previous?.Invoke(combo) ?? false);
    }

    private bool CycleLayout()
    {
        var player = capi?.World?.Player;
        if (player is null || !HandsAreEmpty(player))
        {
            return false;
        }

        var selection = player.CurrentBlockSelection;
        if (selection is null)
        {
            return false;
        }

        var pile = CollectibleBehaviorBookPileable.FindTargetPile(capi!.World, selection);
        if (pile is null)
        {
            return false;
        }

        var next = BookPileUtil.NextLayoutMode(pile.LayoutMode);

        // Apply locally so the pile redraws on the same frame; the server confirms or bounces it.
        pile.SetLayoutMode(next);
        capi.Network.SendBlockEntityPacket(
            pile.Pos,
            BlockEntityBookPile.PacketIdSetLayout,
            BitConverter.GetBytes((int)next));

        capi.ShowChatMessage(Lang.Get(
            "liberterra:bookpile-layout-changed",
            Lang.Get("liberterra:bookpile-layout-" + next.ToString().ToLowerInvariant())));

        return true;
    }

    private static bool HandsAreEmpty(IClientPlayer player)
    {
        var active = player.InventoryManager?.ActiveHotbarSlot;
        var offhand = player.Entity?.LeftHandItemSlot;
        return (active?.Empty ?? true) && (offhand?.Empty ?? true);
    }
}
