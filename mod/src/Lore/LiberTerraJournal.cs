using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace LiberTerra.Lore;

/// <summary>
/// Makes sure every Liber Terra volume is in vanilla's journal lookup before a book is read.
///
/// Reading a lore book pushes <c>loreDiscovery</c>. Vanilla <see cref="ModJournal.TryDiscoverLore"/>
/// then does <c>journalAssetsByCode[code]</c> — a missing key throws, the book GUI still opens, and
/// nothing is written to the J-key journal. The catalog is the source of truth for those codes;
/// <c>GetMany("config/lore/")</c> can miss files (637 volumes in one folder) and never register
/// them. Republic is late in that folder, which matches a report that those books read but never
/// appear in the journal while earlier works do.
/// </summary>
public static class LiberTerraJournal
{
    public const double DiscoveryPriority = 0.6;

    private static readonly MethodInfo? EnsureLoaded = typeof(ModJournal).GetMethod(
        "ensureJournalAssetsLoaded",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo? AssetsByCode = typeof(ModJournal).GetField(
        "journalAssetsByCode",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static void Register(ICoreServerAPI api, Func<LiberTerraCatalog?> catalogProvider)
    {
        // After every StartServerSide, including ModJournal's: its sapi is set, and no book
        // has been read yet. The loreDiscovery listener is the same seed if a save never
        // fired SaveGameLoaded in this process (dev commands, late catalog reload).
        api.Event.SaveGameLoaded += () =>
        {
            try
            {
                Seed(api, catalogProvider());
            }
            catch (Exception exception)
            {
                api.Logger.Error("Liber Terra journal: seed failed: {0}", exception);
            }
        };

        EventBusListenerDelegate onDiscovery = (string eventName, ref EnumHandling handling, IAttribute data) =>
        {
            try
            {
                Seed(api, catalogProvider());
            }
            catch (Exception exception)
            {
                api.Logger.Error("Liber Terra journal: seed failed: {0}", exception);
            }
        };

        api.Event.RegisterEventBusListener(onDiscovery, DiscoveryPriority, "loreDiscovery");
    }

    public static int Seed(ICoreAPI api, LiberTerraCatalog? catalog)
    {
        if (catalog is null || catalog.Works.Count == 0)
        {
            return 0;
        }

        var journal = api.ModLoader.GetModSystem<ModJournal>();
        if (journal is null)
        {
            api.Logger.Warning("Liber Terra journal: ModJournal is not loaded; lore will not record.");
            return 0;
        }

        try
        {
            EnsureLoaded?.Invoke(journal, null);
        }
        catch (Exception exception)
        {
            api.Logger.Error("Liber Terra journal: failed to load vanilla lore assets: {0}", exception);
            return 0;
        }

        if (AssetsByCode?.GetValue(journal) is not Dictionary<string, JournalAsset> assets)
        {
            api.Logger.Warning(
                "Liber Terra journal: could not reach journalAssetsByCode; volumes may not record.");
            return 0;
        }

        var added = 0;
        foreach (var work in catalog.Works)
        {
            if (assets.ContainsKey(work.Code))
            {
                continue;
            }

            assets[work.Code] = new JournalAsset
            {
                Code = work.Code,
                Category = work.Code,
                Title = work.TitleCode,
                Pieces = work.TextCodes.ToArray()
            };
            added++;
        }

        if (added > 0)
        {
            api.Logger.Event(
                "Liber Terra journal: registered {0} missing lore volume(s) ({1} total)",
                added,
                catalog.Works.Count);
        }

        return added;
    }
}
