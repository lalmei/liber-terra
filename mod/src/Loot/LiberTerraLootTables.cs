using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;

namespace LiberTerra.Loot;

/// <summary>
/// Turns the collection randomizer into real books wherever world loot names it, because Vintage
/// Story only ever resolves loot once.
///
/// A ruin chest is resolved by BlockEntityContainer.OnLoadCollectibleMappings: for each slot it asks
/// the IResolvableCollectible in it for a stack, drops the answer in the slot, and never looks
/// again. ItemStackRandomizer.Resolve does not recurse either. So a randomizer that rolls another
/// randomizer leaves the second one lying in the chest — which is how players ended up holding
/// "Liber Terra: Random Library Book" instead of a book. BlockPan is blunter still: it clones its
/// panning drop straight into the player's inventory and resolves nothing at all.
///
/// So the nesting is done here, while the tables are still JSON. Every entry naming
/// game:stackrandomizer-liberterra-any is replaced by one real book per volume — one of that
/// volume's covers, rolled now — sharing out the chance the marker carried. Vanilla's single
/// resolve then lands on a book, and panning hands one over directly.
///
/// One cover per volume rather than all sixteen: these tables are read into resolved ItemStacks and
/// kept for the whole session, and pan drops are shuffled on every pan. Every volume stays
/// reachable from every source; the cover is re-rolled per table and per launch.
///
/// AssetsFinalize is the moment for this. The server runs it (ModRunPhase.AssetsFinalize) before
/// EnumServerRunPhase.LoadGamePre, where ItemStackRandomizer.OnLoaded and BlockPan.OnLoaded read
/// these attributes.
/// </summary>
public static class LiberTerraLootTables
{
    /// <summary>The generated collection randomizer: every volume in every cover, one roll.</summary>
    public static readonly AssetLocation RandomizerCode = new("game", "stackrandomizer-liberterra-any");

    private const string StacksKey = "stacks";
    private const string PanningDropsKey = "panningDrops";
    private const string CategoryKey = "category";
    private const string CodeKey = "code";
    private const string TypeKey = "type";
    private const string AttributesKey = "attributes";
    private const string ChanceKey = "chance";

    public static void Expand(ICoreAPI api)
    {
        var volumes = ReadVolumes(api);
        if (volumes.Count == 0)
        {
            api.Logger.Warning(
                "Liber Terra loot: {0} has no stacks to hand out; ruin and panning loot would give the randomizer itself.",
                RandomizerCode);
            return;
        }

        var rand = new Random();
        var tables = 0;
        var markers = 0;
        var books = 0;

        foreach (var collectible in LootTableHolders(api))
        {
            if (collectible.Attributes?.Token is not JObject attributes)
            {
                continue;
            }

            foreach (var table in LootTables(attributes))
            {
                var expanded = ExpandTable(table, volumes, rand);
                if (expanded.Markers == 0)
                {
                    continue;
                }

                tables++;
                markers += expanded.Markers;
                books += expanded.Books;
            }
        }

        if (markers == 0)
        {
            return;
        }

        api.Logger.Event(
            "Liber Terra loot: expanded {0} randomizer entr(ies) in {1} loot table(s) into {2} book stack(s) from {3} volume(s)",
            markers,
            tables,
            books,
            volumes.Count);
    }

    /// <summary>Items hold the ruin chest randomizers, blocks hold the pan.</summary>
    private static IEnumerable<CollectibleObject> LootTableHolders(ICoreAPI api)
    {
        foreach (var item in api.World.Items)
        {
            if (item is not null)
            {
                yield return item;
            }
        }

        foreach (var block in api.World.Blocks)
        {
            if (block is not null)
            {
                yield return block;
            }
        }
    }

    /// <summary>
    /// The two shapes world loot uses: an ItemStackRandomizer's stacks, and a pan's drops per
    /// source material.
    /// </summary>
    internal static IEnumerable<JArray> LootTables(JObject attributes)
    {
        if (Property(attributes, StacksKey) is JArray stacks)
        {
            yield return stacks;
        }

        if (Property(attributes, PanningDropsKey) is not JObject panningDrops)
        {
            yield break;
        }

        foreach (var sourceMaterial in panningDrops.Properties())
        {
            if (sourceMaterial.Value is JArray drops)
            {
                yield return drops;
            }
        }
    }

    internal static (int Markers, int Books) ExpandTable(
        JArray table,
        IReadOnlyList<IReadOnlyList<JObject>> volumes,
        Random rand)
    {
        var markers = 0;
        var books = 0;

        for (var i = table.Count - 1; i >= 0; i--)
        {
            if (table[i] is not JObject entry || !IsRandomizer(entry))
            {
                continue;
            }

            var rolled = RollOneCoverPerVolume(entry, volumes, rand);
            table.RemoveAt(i);
            for (var j = rolled.Count - 1; j >= 0; j--)
            {
                table.Insert(i, rolled[j]);
            }

            markers++;
            books += rolled.Count;
        }

        return (markers, books);
    }

    /// <summary>
    /// One book per volume, keeping whatever else the loot entry said (quantity, lastDrop, tool)
    /// and splitting its chance so the collection weighs the same as the single randomizer did.
    /// </summary>
    private static List<JObject> RollOneCoverPerVolume(
        JObject marker,
        IReadOnlyList<IReadOnlyList<JObject>> volumes,
        Random rand)
    {
        var books = new List<JObject>(volumes.Count);

        foreach (var covers in volumes)
        {
            var cover = covers[rand.Next(covers.Count)];
            var book = (JObject)marker.DeepClone();

            book[TypeKey] = cover[TypeKey]?.DeepClone() ?? "item";
            book[CodeKey] = cover[CodeKey]!.DeepClone();

            if (cover[AttributesKey] is { } attributes)
            {
                book[AttributesKey] = attributes.DeepClone();
            }
            else
            {
                book.Remove(AttributesKey);
            }

            ShareChance(book, volumes.Count);
            books.Add(book);
        }

        return books;
    }

    /// <summary>
    /// Chest randomizers weigh their stacks against each other, the pan rolls each drop on its own
    /// NatFloat — dividing works for both.
    /// </summary>
    private static void ShareChance(JObject entry, int parts)
    {
        if (parts <= 1)
        {
            return;
        }

        switch (Property(entry, ChanceKey))
        {
            case JValue chance when IsNumber(chance):
                entry[ChanceKey] = chance.Value<double>() / parts;
                break;

            case JObject natFloat:
                foreach (var field in natFloat.Properties().ToList())
                {
                    if (field.Value is JValue value && IsNumber(value))
                    {
                        natFloat[field.Name] = value.Value<double>() / parts;
                    }
                }

                break;
        }
    }

    private static List<IReadOnlyList<JObject>> ReadVolumes(ICoreAPI api)
    {
        return api.World.GetItem(RandomizerCode)?.Attributes?.Token is JObject attributes
            ? GroupVolumes(attributes)
            : [];
    }

    /// <summary>
    /// Every volume the collection randomizer can roll, with the covers it can roll it in. Reading
    /// the randomizer's own stacks keeps the generated asset the single source of truth.
    /// </summary>
    internal static List<IReadOnlyList<JObject>> GroupVolumes(JObject randomizerAttributes)
    {
        if (Property(randomizerAttributes, StacksKey) is not JArray stacks)
        {
            return [];
        }

        var covers = new Dictionary<string, List<JObject>>(StringComparer.Ordinal);
        var volumes = new List<IReadOnlyList<JObject>>();

        foreach (var token in stacks)
        {
            if (token is not JObject stack)
            {
                continue;
            }

            var code = stack[CodeKey]?.Value<string>();
            if (string.IsNullOrEmpty(code))
            {
                continue;
            }

            var volume = stack[AttributesKey]?[CategoryKey]?.Value<string>() ?? code;
            if (!covers.TryGetValue(volume, out var rolls))
            {
                rolls = [];
                covers[volume] = rolls;
                volumes.Add(rolls);
            }

            rolls.Add(stack);
        }

        return volumes;
    }

    internal static bool IsRandomizer(JObject entry)
    {
        var code = entry[CodeKey]?.Value<string>();
        return !string.IsNullOrEmpty(code)
               && AssetLocation.Create(code, RandomizerCode.Domain).Equals(RandomizerCode);
    }

    private static bool IsNumber(JValue value) =>
        value.Type is JTokenType.Float or JTokenType.Integer;

    private static JToken? Property(JObject holder, string name) =>
        holder.Property(name, StringComparison.OrdinalIgnoreCase)?.Value;
}
