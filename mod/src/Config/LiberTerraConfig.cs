using Vintagestory.API.Common;

namespace LiberTerra.Config;

/// <summary>
/// Server-owned settings, stored in <c>VintagestoryData/ModConfig/liberterra.json</c>.
///
/// The server is the only side that reads the file. Clients are handed a copy on join
/// (see <see cref="Network.LiberTerraNetwork"/>) because the throw/read decision is made
/// client-side, where the mouse actually is — a client running on stale settings would arm a
/// windup the server then refuses to finish.
/// </summary>
public sealed class LiberTerraConfig
{
    public const string FileName = "liberterra.json";

    /// <summary>
    /// Whether holding right mouse on a book winds up a throw. Off means right mouse only reads,
    /// with no aim pose and no charge — the pre-throw behaviour.
    /// </summary>
    public bool EnableBookThrowing { get; set; } = true;

    /// <summary>
    /// How long right mouse must be held before a release throws instead of reads.
    /// </summary>
    public float ThrowWindupSeconds { get; set; } = Items.BookThrowUtil.DefaultWindupSec;

    /// <summary>Clamped so a corrupt file cannot make every click a throw.</summary>
    public float WindupSeconds => Math.Clamp(ThrowWindupSeconds, 0.2f, 10f);

    public static LiberTerraConfig LoadOrCreate(ICoreAPI api)
    {
        LiberTerraConfig? loaded = null;
        try
        {
            loaded = api.LoadModConfig<LiberTerraConfig>(FileName);
        }
        catch (Exception exception)
        {
            api.Logger.Error("Liber Terra config failed to load, using defaults: {0}", exception);
        }

        var config = loaded ?? new LiberTerraConfig();
        config.Save(api);
        return config;
    }

    public void Save(ICoreAPI api)
    {
        try
        {
            api.StoreModConfig(this, FileName);
        }
        catch (Exception exception)
        {
            api.Logger.Error("Liber Terra config failed to save: {0}", exception);
        }
    }

    /// <summary>
    /// The client's copy of the server settings, for places that have no entity to ask — held-item
    /// interaction help, which only ever renders on the client. Never read this on the server.
    /// </summary>
    public static LiberTerraConfig Current { get; set; } = new();

    /// <summary>
    /// The settings in force on this side. Held per mod system instance rather than in a static,
    /// so a singleplayer client applying the server's copy cannot overwrite the server's own.
    /// </summary>
    public static LiberTerraConfig For(Vintagestory.API.Common.Entities.Entity entity)
        => entity.Api?.ModLoader?.GetModSystem<LiberTerraModSystem>()?.Config ?? Fallback;

    private static readonly LiberTerraConfig Fallback = new();
}
