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

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        RegisterCompleteBooksInCreative(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        new LiberTerraServerCommands(() => catalog).Register(api);
    }

    private void RegisterCompleteBooksInCreative(ICoreAPI api)
    {
        if (catalog is null || catalog.Works.Count == 0)
        {
            return;
        }

        var host = api.World.GetItem(new AssetLocation("liberterra", "library"));
        if (host is null)
        {
            api.Logger.Warning("Liber Terra creative host item liberterra:library is missing.");
            return;
        }

        var stacks = new JsonItemStack[catalog.Works.Count];
        for (var i = 0; i < catalog.Works.Count; i++)
        {
            var book = LiberTerraServerCommands.CreateCompleteBook(api, catalog.Works[i]);
            stacks[i] = new JsonItemStack
            {
                Type = EnumItemClass.Item,
                Code = book.Collectible.Code,
                ResolvedItemstack = book
            };
        }

        host.CreativeInventoryStacks =
        [
            new CreativeTabAndStackList
            {
                Tabs = ["liberterra"],
                Stacks = stacks
            }
        ];

        api.Logger.Event(
            "Liber Terra creative shelf registered: volumes={0}",
            stacks.Length);
    }
}
