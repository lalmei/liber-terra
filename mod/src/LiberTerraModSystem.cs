using LiberTerra.Commands;
using LiberTerra.Lore;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace LiberTerra;

public sealed class LiberTerraModSystem : ModSystem
{
    private LiberTerraCatalog? catalog;

    public LiberTerraCatalog? Catalog => catalog;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(LiberTerraModMetadata.StartupLogMessage);
    }

    public override void AssetsLoaded(ICoreAPI api)
    {
        try
        {
            catalog = LiberTerraCatalog.Load(api, "liberterra:config/liberterra-catalog.json");
            api.Logger.Event(
                "Liber Terra catalog loaded: volumes={0}",
                catalog.Works.Count);
        }
        catch (Exception exception)
        {
            api.Logger.Error("Liber Terra catalog failed to load: {0}", exception);
            catalog = null;
        }
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        new LiberTerraServerCommands(() => catalog).Register(api);
    }
}
