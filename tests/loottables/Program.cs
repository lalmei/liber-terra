// Checks the loot table expansion in mod/src/Loot/LiberTerraLootTables.cs.
//
// The bug this guards against was invisible to either side alone: the patches named a randomizer
// inside a randomizer, which is valid JSON and loads without a murmur, but Vintage Story resolves
// loot once per slot — so ruin chests and pans handed players the "Random Library Book" crate
// itself. These cases run the expansion over the real generated randomizer asset and over replicas
// of the vanilla tables it is patched into, and assert a book comes out with the odds intact.
//
// No test framework on purpose: the mod has no NuGet dependencies and this needs none. Run it with
// `make test-loot`; it exits nonzero on the first failing expectation.

using LiberTerra.Loot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.GameContent;

var root = RepositoryRoot();
var failures = 0;
var checks = 0;

// --- the volumes every loot source draws from ---------------------------------------------------

Case("generated collection randomizer");

var randomizerAsset = ReadJson<JObject>("mod/assets/game/itemtypes/meta/stackrandomizer-liberterra-loot.json");
var randomizerAttributes = (JObject)randomizerAsset["attributesByType"]!["*-liberterra-any"]!;
var volumes = LiberTerraLootTables.GroupVolumes(randomizerAttributes);
var catalogWorks = ((JArray)ReadJson<JObject>("mod/assets/liberterra/config/liberterra-catalog.json")["works"]!).Count;

Check("a volume for every catalogued work", volumes.Count == catalogWorks,
    $"{volumes.Count} volumes, {catalogWorks} works");
Check("every volume has covers to roll", volumes.All(covers => covers.Count > 0),
    $"{volumes.Min(covers => covers.Count)}-{volumes.Max(covers => covers.Count)} covers per volume");
Check("every stack is a real lore book",
    volumes.SelectMany(covers => covers).All(cover => Code(cover).StartsWith("game:lore-book-")));
Check("no volume rolls a randomizer",
    !volumes.SelectMany(covers => covers).Any(LiberTerraLootTables.IsRandomizer));

// --- the crate players are already holding --------------------------------------------------------

Case("opening a crate the expansion never got to");

// The expansion only reaches loot that has yet to be rolled. Chunks generated before it, and crates
// already in inventories, keep the unresolved item forever — vanilla resolves a slot once and that
// slot's answer was the crate. ItemLiberTerraStackRandomizer is how those still turn into books, so
// the generated asset has to name it and Start() has to register it under the same name; miss either
// and the item quietly loads as something else.
var randomizerClass = randomizerAsset["class"]?.Value<string>() ?? "";

Check("the generated asset names the mod's own randomizer class",
    randomizerClass == nameof(ItemLiberTerraStackRandomizer), randomizerClass);
Check("which is still a vanilla randomizer, so chests resolve it exactly as before",
    typeof(ItemLiberTerraStackRandomizer).IsSubclassOf(typeof(ItemStackRandomizer)));
// Read as source because registration needs a running ICoreAPI, which a unit test has no business
// standing up: the name in the JSON and the name in Start() are the whole contract.
Check("and the mod registers that name",
    ReadText("mod/src/LiberTerraModSystem.cs").Contains($"RegisterItemClass(\"{randomizerClass}\""),
    "otherwise the crate loads as a plain item and never opens");

// --- the patches that inject it into world loot --------------------------------------------------

Case("shipped loot patches");

string[] patchFiles =
[
    "mod/assets/liberterra/patches/stackrandomizer-vanilla-lore.json",
    "mod/assets/liberterra/patches/pan-bonysoil-liberterra.json",
    "mod/assets/liberterra/compatibility/betterruins/patches/stackrandomizer-newlore.json",
];

var patches = patchFiles.SelectMany(file => ReadJson<JArray>(file).Cast<JObject>()).ToList();

Check("patches found", patches.Count > 0, $"{patches.Count} injections across {patchFiles.Length} files");
Check("every injection is one the expansion recognises",
    patches.All(patch => patch["value"] is JObject value && LiberTerraLootTables.IsRandomizer(value)));
Check("every injection lands in a table shape the expansion walks",
    patches.All(patch => Path(patch).Contains("/stacks/") || Path(patch).Contains("/panningDrops/")),
    "otherwise the marker is never expanded and players get the crate");
Check("injections carry a chance to share out",
    patches.All(patch => patch["value"]!["chance"] is not null));

// --- vanilla ruin chest lore pool -----------------------------------------------------------------

Case("vanilla *-lore-villager (chance is a relative weight)");

var villager = JArray.Parse("""
[
  { "type": "item", "code": "lore-scroll", "chance": 1, "attributes": { "category": "villager" } },
  { "type": "item", "code": "stackrandomizer-liberterra-any", "chance": 0.5 }
]
""");
var expanded = LiberTerraLootTables.ExpandTable(villager, volumes, new Random(1234));
var books = villager.Skip(1).Cast<JObject>().ToList();

Check("the marker expanded", expanded.Markers == 1 && expanded.Books == volumes.Count,
    $"markers={expanded.Markers}, books={expanded.Books}");
Check("nothing rolls a randomizer any more", !villager.Cast<JObject>().Any(LiberTerraLootTables.IsRandomizer));
Check("the vanilla scroll survives untouched",
    villager.Count == volumes.Count + 1 && Code((JObject)villager[0]) == "lore-scroll");
Check("the marker's weight is preserved",
    Math.Abs(books.Sum(book => book["chance"]!.Value<double>()) - 0.5) < 1e-9,
    $"sum={books.Sum(book => book["chance"]!.Value<double>())}");
Check("every volume stays reachable, once",
    books.Select(Category).Distinct().Count() == volumes.Count);
Check("covers vary across the collection",
    books.Select(Code).Distinct().Count() > 1,
    $"{books.Select(Code).Distinct().Count()} distinct covers");
Check("expanding an expanded table changes nothing",
    LiberTerraLootTables.ExpandTable(villager, volumes, new Random(1234)) is { Markers: 0, Books: 0 });
Note(books[0].ToString(Formatting.None));

// --- bony soil panning ------------------------------------------------------------------------------

Case("pan panningDrops bonysoil (chance is a NatFloat, rolled per drop)");

var pan = JArray.Parse("""
[
  { "type": "item", "code": "bone", "chance": { "avg": 0.3, "var": 0 } },
  { "type": "item", "code": "stackrandomizer-liberterra-any", "chance": { "avg": 0.01, "var": 0 } }
]
""");
var panExpanded = LiberTerraLootTables.ExpandTable(pan, volumes, new Random(1234));
var panBooks = pan.Skip(1).Cast<JObject>().ToList();
var panRate = 1 - panBooks.Aggregate(1.0, (miss, book) => miss * (1 - book["chance"]!["avg"]!.Value<double>()));

Check("the marker expanded", panExpanded.Markers == 1);
Check("the vanilla drop is untouched", pan[0]["chance"]!["avg"]!.Value<double>() == 0.3);
Check("a book is still a 1-in-100 pan", Math.Abs(panRate - 0.01) < 1e-4, $"P(book)={panRate:0.#####}");
Check("the NatFloat keeps its shape", panBooks.All(book => book["chance"]!["var"]!.Value<double>() == 0));

// --- better ruins, and loot fields we do not own ------------------------------------------------------

Case("betterruins *-newlore (domain-qualified, extra loot fields)");

var betterRuins = JArray.Parse("""
[
  { "type": "item", "code": "game:stackrandomizer-liberterra-any", "chance": 8, "quantity": { "avg": 1, "var": 0 }, "lastDrop": true }
]
""");
var brExpanded = LiberTerraLootTables.ExpandTable(betterRuins, volumes, new Random(1234));

Check("a domain-qualified marker is matched", brExpanded.Markers == 1);
Check("the weight is shared out",
    Math.Abs(betterRuins.Sum(entry => entry["chance"]!.Value<double>()) - 8) < 1e-9);
Check("other loot fields ride along",
    betterRuins.All(entry => entry["lastDrop"]!.Value<bool>() && entry["quantity"]!["avg"]!.Value<double>() == 1));

// --- what must not be touched --------------------------------------------------------------------------

Case("tables that are none of our business");

var vanillaOnly = JArray.Parse("""
[
  { "type": "item", "code": "stackrandomizer-lore-villager", "chance": 1 },
  { "type": "item", "code": "game:lore-book-aged-gray", "chance": 1, "attributes": { "category": "beowulf-vol1" } }
]
""");
var before = vanillaOnly.ToString();

Check("left exactly as they were",
    LiberTerraLootTables.ExpandTable(vanillaOnly, volumes, new Random(1234)).Markers == 0
    && vanillaOnly.ToString() == before);

// --- both table shapes are found on a collectible ---------------------------------------------------------

Case("finding the tables on a collectible");

var randomizerItem = JObject.Parse("""{ "handbook": { "exclude": true }, "stacks": [] }""");
var panBlock = JObject.Parse("""{ "panningDrops": { "bonysoil": [], "@(sand|sand-.*)": [] } }""");

Check("an item's stacks are a table", LiberTerraLootTables.LootTables(randomizerItem).Count() == 1);
Check("a pan's drops are a table per source material",
    LiberTerraLootTables.LootTables(panBlock).Count() == 2);
Check("attributes with neither offer nothing",
    !LiberTerraLootTables.LootTables(JObject.Parse("""{ "burnTemperature": 800 }""")).Any());

Console.WriteLine(failures == 0
    ? $"\n{checks} checks passed"
    : $"\n{failures} of {checks} checks FAILED");
return failures == 0 ? 0 : 1;

void Case(string name) => Console.WriteLine($"\n{name}");

void Check(string what, bool held, string detail = "")
{
    checks++;
    if (!held)
    {
        failures++;
    }

    Console.WriteLine($"  [{(held ? "ok" : "FAIL")}] {what}{(detail.Length > 0 ? " — " + detail : "")}");
}

void Note(string detail) => Console.WriteLine($"         {detail}");

string Code(JObject entry) => entry["code"]?.Value<string>() ?? "";

string Category(JObject entry) => entry["attributes"]?["category"]?.Value<string>() ?? "";

string Path(JObject patch) => patch["path"]?.Value<string>() ?? "";

T ReadJson<T>(string relativePath) where T : JToken => (T)JToken.Parse(ReadText(relativePath));

string ReadText(string relativePath) =>
    File.ReadAllText(System.IO.Path.Combine(root, relativePath));

static string RepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "LiberTerra.sln")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
}
