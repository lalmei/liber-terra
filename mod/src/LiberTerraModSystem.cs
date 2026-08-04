using LiberTerra.Commands;
using LiberTerra.Items;
using LiberTerra.Lore;
using LiberTerra.Storage;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LiberTerra;

public sealed class LiberTerraModSystem : ModSystem
{
    private LiberTerraCatalog? catalog;

    public LiberTerraCatalog? Catalog => catalog;

    public override void Start(ICoreAPI api)
    {
        api.Logger.Event(LiberTerraModMetadata.StartupLogMessage);
        api.RegisterItemClass("ItemLiberTerraLoreBook", typeof(ItemLiberTerraLoreBook));
        api.RegisterItemClass("ItemBookStack", typeof(ItemBookStack));
        api.RegisterCollectibleBehaviorClass("BookStackable", typeof(CollectibleBehaviorBookStackable));
        api.RegisterCollectibleBehaviorClass("BookThrowable", typeof(CollectibleBehaviorBookThrowable));
        api.RegisterCollectibleBehaviorClass("BookPileable", typeof(CollectibleBehaviorBookPileable));
        api.RegisterBlockClass("BlockBookPile", typeof(BlockBookPile));
        api.RegisterBlockEntityClass("BookPile", typeof(BlockEntityBookPile));
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
        PreferBookPileOverGroundStorage(api);
        EnsureBookStackable(api);
        EnsureBookThrowable(api);
        RegisterCompleteBooksInCreative(api);
    }

    /// <summary>
    /// Lore books ship with vanilla GroundStorable (Quadrants). Strip it so BookPileable owns floor placement.
    /// Paper/scroll variants keep GroundStorable.
    /// </summary>
    private static void PreferBookPileOverGroundStorage(ICoreAPI api)
    {
        var stripped = 0;
        foreach (var item in api.World.Items)
        {
            if (item?.Code?.Path is null || !item.Code.Path.StartsWith("lore-book-", StringComparison.Ordinal))
            {
                continue;
            }

            if (item.CollectibleBehaviors is null || item.CollectibleBehaviors.Length == 0)
            {
                continue;
            }

            var before = item.CollectibleBehaviors.Length;
            item.CollectibleBehaviors = item.CollectibleBehaviors
                .Where(behavior => behavior is not CollectibleBehaviorGroundStorable)
                .ToArray();

            if (item.CollectibleBehaviors.Length != before)
            {
                stripped++;
            }

            if (!item.HasBehavior<CollectibleBehaviorBookPileable>())
            {
                var behavior = new CollectibleBehaviorBookPileable(item);
                behavior.Initialize(new Vintagestory.API.Datastructures.JsonObject(
                    Newtonsoft.Json.Linq.JObject.Parse("{}")));
                item.CollectibleBehaviors = item.CollectibleBehaviors.Append(behavior).ToArray();
            }
        }

        if (stripped > 0)
        {
            api.Logger.Event(
                "Liber Terra book piles: removed GroundStorable from {0} lore-book variant(s)",
                stripped);
        }
    }

    /// <summary>
    /// Ensure lore-book variants have BookStackable even if the JSON patch did not apply.
    /// </summary>
    private static void EnsureBookStackable(ICoreAPI api)
    {
        var attached = 0;
        foreach (var item in api.World.Items)
        {
            if (item?.Code?.Path is null || !item.Code.Path.StartsWith("lore-book-", StringComparison.Ordinal))
            {
                continue;
            }

            if (item.CollectibleBehaviors is null)
            {
                item.CollectibleBehaviors = [];
            }

            if (item.HasBehavior<CollectibleBehaviorBookStackable>())
            {
                continue;
            }

            var behavior = new CollectibleBehaviorBookStackable(item);
            behavior.Initialize(new Vintagestory.API.Datastructures.JsonObject(
                Newtonsoft.Json.Linq.JObject.Parse("{}")));
            item.CollectibleBehaviors = item.CollectibleBehaviors.Append(behavior).ToArray();
            attached++;
        }

        if (attached > 0)
        {
            api.Logger.Event(
                "Liber Terra book stacks: attached BookStackable to {0} lore-book variant(s)",
                attached);
        }
    }

    /// <summary>
    /// Ensure lore-book variants have BookThrowable even if the JSON patch did not apply.
    /// </summary>
    private static void EnsureBookThrowable(ICoreAPI api)
    {
        foreach (var item in api.World.Items)
        {
            if (item?.Code?.Path is null || !item.Code.Path.StartsWith("lore-book-", StringComparison.Ordinal))
            {
                continue;
            }

            if (item.CollectibleBehaviors is null)
            {
                item.CollectibleBehaviors = [];
            }

            if (item.HasBehavior<CollectibleBehaviorBookThrowable>())
            {
                continue;
            }

            var throwBehavior = new CollectibleBehaviorBookThrowable(item);
            throwBehavior.Initialize(new Vintagestory.API.Datastructures.JsonObject(
                Newtonsoft.Json.Linq.JObject.Parse(
                    """{"damage":1,"thrownProjectileCode":"thrownitem","damageType":"BluntAttack","randomThrowYaw":true,"dropOnImpactChance":1}""")));
            item.CollectibleBehaviors = item.CollectibleBehaviors.Append(throwBehavior).ToArray();
        }
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
