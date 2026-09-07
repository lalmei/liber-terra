using LiberTerra.Config;
using LiberTerra.Items;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace LiberTerra.Network;

/// <summary>
/// Sent by the client when a held-book use ends, saying which of the two actions the player asked
/// for. The client is the only side that can tell them apart: see <see cref="BookThrowUtil"/>.
/// </summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class BookUsePacket
{
    public bool ThrowIt { get; set; }
}

/// <summary>Server settings pushed to a joining client, and again whenever they change.</summary>
[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public sealed class LiberTerraConfigPacket
{
    public bool EnableBookThrowing { get; set; } = true;
    public float ThrowWindupSeconds { get; set; } = BookThrowUtil.DefaultWindupSec;
}

/// <summary>
/// The one channel the mod needs: book uses travel client to server, settings travel back.
/// </summary>
public sealed class LiberTerraNetwork
{
    public const string ChannelName = "liberterra";

    private IClientNetworkChannel? clientChannel;
    private IServerNetworkChannel? serverChannel;

    public void StartClientSide(ICoreClientAPI api, Action<LiberTerraConfigPacket> onConfig)
    {
        clientChannel = api.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<BookUsePacket>()
            .RegisterMessageType<LiberTerraConfigPacket>()
            .SetMessageHandler<LiberTerraConfigPacket>(packet => onConfig(packet));
    }

    public void StartServerSide(ICoreServerAPI api, Func<LiberTerraConfig> config)
    {
        serverChannel = api.Network
            .RegisterChannel(ChannelName)
            .RegisterMessageType<BookUsePacket>()
            .RegisterMessageType<LiberTerraConfigPacket>()
            .SetMessageHandler<BookUsePacket>((player, packet) =>
                BookUseServer.Resolve(player, packet.ThrowIt, config()));

        api.Event.PlayerJoin += player => SendConfig(config(), player);
    }

    /// <summary>Tells the server how a book use ended. No-op off the client.</summary>
    public void SendBookUse(bool throwIt)
        => clientChannel?.SendPacket(new BookUsePacket { ThrowIt = throwIt });

    public void SendConfig(LiberTerraConfig config, params IServerPlayer[] players)
    {
        var packet = new LiberTerraConfigPacket
        {
            EnableBookThrowing = config.EnableBookThrowing,
            ThrowWindupSeconds = config.WindupSeconds
        };

        if (players.Length > 0)
        {
            serverChannel?.SendPacket(packet, players);
        }
        else
        {
            serverChannel?.BroadcastPacket(packet);
        }
    }
}

/// <summary>
/// Carries out on the server what the client decided: throw the held book, or read it.
/// </summary>
public static class BookUseServer
{
    public static void Resolve(IServerPlayer player, bool throwIt, LiberTerraConfig config)
    {
        var entity = player.Entity;
        var slot = player.InventoryManager?.ActiveHotbarSlot;
        if (entity is null || slot?.Itemstack is null)
        {
            return;
        }

        // The windup pose is server state too, and this release is the only news of its end.
        BookThrowUtil.StopAiming(entity);

        var wantsThrow = throwIt && config.EnableBookThrowing;
        var blockSel = player.CurrentBlockSelection;
        var entitySel = player.CurrentEntitySelection;

        if (slot.Itemstack.Collectible is ItemBookStack bookStack)
        {
            bookStack.ResolveHeldUse(slot, entity, wantsThrow, blockSel, entitySel);
            return;
        }

        if (!BookCodes.IsThrowableBook(slot.Itemstack))
        {
            return;
        }

        if (!wantsThrow)
        {
            BookThrowUtil.TryOpenBook(slot.Itemstack.Collectible, slot, entity, blockSel, entitySel);
            return;
        }

        var thrown = slot.TakeOut(1);
        slot.MarkDirty();
        BookThrowUtil.TryThrowItemStack(
            entity,
            thrown,
            BookThrowUtil.DefaultBookProjectileConfig());
    }
}
