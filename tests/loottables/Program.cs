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

using LiberTerra;
using LiberTerra.Loot;
using LiberTerra.Storage;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
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

// --- writable books keep their vanilla editor -----------------------------------------------------

Case("throwing player-written books");

var emptyWritableBook = BookStack("book-normal-brickred", editable: true);
var unsignedDraft = emptyWritableBook.Clone();
unsignedDraft.Attributes.SetString("text", "A draft");
var signedBook = unsignedDraft.Clone();
signedBook.Attributes.SetString("signedby", "Ada");

Check("an empty writable book is left to the vanilla editor",
    !BookCodes.IsThrowableBook(emptyWritableBook));
Check("an unsigned draft can still be edited",
    !BookCodes.IsThrowableBook(unsignedDraft));
Check("a signed book can be thrown",
    BookCodes.IsThrowableBook(signedBook));
Check("a found lore book can be thrown",
    BookCodes.IsThrowableBook(BookStack("lore-book-aged-gray", editable: false)));

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

// --- decorative book clutter becomes real inventories -----------------------------------------------

Case("vanilla book clutter conversion");

var pileCounts = new[] { 16, 12, 8, 7, 17 };
var pileLayouts = new[]
{
    BookPileLayoutMode.Messy,
    BookPileLayoutMode.Uneven,
    BookPileLayoutMode.Bridged,
    BookPileLayoutMode.Scattered,
    BookPileLayoutMode.Tumbled
};
var stackCounts = new[] { 16, 13, 8, 9 };
var rowCounts = new[] { 6, 7, 6, 7, 8, 6, 9, 7, 12, 12, 9, 12, 14, 10, 3 };

Check("all five vanilla bookpiles retain their count and traced layout",
    Enumerable.Range(1, pileCounts.Length).All(number =>
        BookClutterConversion.TryGetSpec($"bookshelves/bookpile{number}", out var spec)
        && spec.BookCount == pileCounts[number - 1]
        && spec.Layout == pileLayouts[number - 1]));
Check("all five aged bookpiles map to the same real piles",
    Enumerable.Range(1, pileCounts.Length).All(number =>
        BookClutterConversion.TryGetSpec($"bookshelves/bookpile-aged{number}", out var spec)
        && spec.BookCount == pileCounts[number - 1]
        && spec.Layout == pileLayouts[number - 1]));
Check("the two evaporating aged variants are covered",
    BookClutterConversion.TryGetSpec("bookshelves/bookpile-aged2-evaporating", out var agedTwo)
    && agedTwo.BookCount == 12
    && BookClutterConversion.TryGetSpec("bookshelves/bookpile-aged5-evaporating", out var agedFive)
    && agedFive.BookCount == 17);
Check("all four bookstacks become neat piles with the authored count",
    Enumerable.Range(1, stackCounts.Length).All(number =>
        BookClutterConversion.TryGetSpec($"bookshelves/bookstack{number}", out var spec)
        && spec.BookCount == stackCounts[number - 1]
        && spec.Layout == BookPileLayoutMode.Neat));
Check("all fifteen bookrows become shelved piles with the authored count",
    Enumerable.Range(1, rowCounts.Length).All(number =>
        BookClutterConversion.TryGetSpec($"bookrow/bookrow{number}", out var spec)
        && spec.BookCount == rowCounts[number - 1]
        && spec.Layout == BookPileLayoutMode.Shelved));
Check("similarly named non-target clutter stays vanilla",
    new[]
    {
        "bookshelves/large-book-pile1",
        "bookshelves/bookpile1-evaporating",
        "bookshelves/bookpile-aged1-evaporating",
        "book-big-open",
        "bookrow/bookrow16",
        "scrollrack-full1"
    }.All(type => !BookClutterConversion.TryGetSpec(type, out _)));

var clutterPatch = ReadJson<JArray>("mod/assets/liberterra/patches/clutter-book-converter.json");
Check("the server attaches the converter only through the clutter block entity",
    clutterPatch.Count == 1
    && clutterPatch[0]!["file"]!.Value<string>() == "game:blocktypes/clutter.json"
    && clutterPatch[0]!["path"]!.Value<string>() == "/entityBehaviors/-"
    && clutterPatch[0]!["value"]!["name"]!.Value<string>() == "BookClutterConverter");
Check("the converter is a block-entity behavior",
    typeof(BlockEntityBehaviorBookClutterConverter).IsSubclassOf(typeof(BlockEntityBehavior)));
Check("and the mod registers the patched behavior name",
    ReadText("mod/src/LiberTerraModSystem.cs").Contains(
        "RegisterBlockEntityBehaviorClass(\n            \"BookClutterConverter\""));

Case("book-bearing clutter bookshelves become readable");

var ruinedShelfCounts = new[] { 6, 2, 1, 8, 3, 3, 10, 7, 8, 14, 13, 11, 10, 5, 8, 11, 11 };
var loreShelfCounts = new[] { 5, 7, 9, 7, 3, 2, 1 };
var standardShelfCounts = new[] { 14, 14, 14, 6, 11, 11, 5, 7, 12 };
var fancyShelfCounts = new[] { 14, 11, 12, 12, 9 };
var stuffShelfCounts = new[] { 11, 12, 12, 2, 5, 6, 4, 5, 8, 3, 8 };
Check("the intact full shelf exposes all fourteen modeled books",
    ReadableClutterShelfConversion.TryGetBookCount(
        "bookshelves/bookshelf-full",
        out var fullShelfCount)
    && fullShelfCount == 14);
Check("all nine standard shelf shapes expose every modeled book",
    Enumerable.Range(1, standardShelfCounts.Length).All(number =>
        ReadableClutterShelfConversion.TryGetBookCount(
            $"bookshelves/bookshelf-standard{number}",
            out var count)
        && count == standardShelfCounts[number - 1]));
Check("all five fancy shelf shapes expose every modeled book",
    Enumerable.Range(1, fancyShelfCounts.Length).All(number =>
        ReadableClutterShelfConversion.TryGetBookCount(
            $"bookshelves/bookshelf-fancy{number}",
            out var count)
        && count == fancyShelfCounts[number - 1]));
Check("all eleven mixed-content shelf shapes expose every modeled book",
    Enumerable.Range(1, stuffShelfCounts.Length).All(number =>
        ReadableClutterShelfConversion.TryGetBookCount(
            $"bookshelves/bookshelf-stuff{number:00}",
            out var count)
        && count == stuffShelfCounts[number - 1]));
Check("all seventeen ruined shelf shapes expose every modeled book",
    Enumerable.Range(1, ruinedShelfCounts.Length).All(number =>
        ReadableClutterShelfConversion.TryGetBookCount(
            $"bookshelves/bookshelf-ruined-full{number}",
            out var count)
        && count == ruinedShelfCounts[number - 1]));
Check("all seven vanilla lore shelves expose their ordinary books too",
    Enumerable.Range(1, loreShelfCounts.Length).All(number =>
        ReadableClutterShelfConversion.TryGetBookCount(
            $"bookshelves/bookshelf-ruined-full-lore{number}",
            out var count)
        && count == loreShelfCounts[number - 1]));
Check("empty and similarly named bookshelf shapes remain vanilla",
    new[]
    {
        "bookshelves/bookshelf-ruined-empty1",
        "bookshelves/bookshelf-ruined-full18",
        "bookshelves/bookshelf-ruined-full-lore1-book",
        "bookshelves/bookshelf-empty",
        "bookshelves/bookshelf-fancy-empty",
        "bookshelves/bookshelf-standard10",
        "bookshelves/bookshelf-stuff1",
        "bookshelves/bookshelf-alchemy01",
        "bookshelves/bookshelf-food01",
        "bookshelves/bookshelf-reagents01"
    }.All(type => !ReadableClutterShelfConversion.TryGetBookCount(type, out _)));

var sourceShape = new Shape
{
    Elements =
    [
        new ShapeElement { Name = "shelf", Children = [new ShapeElement { Name = "plank" }] },
        new ShapeElement
        {
            Name = "medium 1",
            Children =
            [
                new ShapeElement { Name = "pages" },
                new ShapeElement { Name = "cover front" }
            ]
        }
    ]
};
var shelfOnlyShape = ReadableClutterShelfMesh.RemoveBakedBooks(sourceShape);
Check("mesh conversion removes a modeled book root but preserves the shelf",
    sourceShape.Elements.Length == 2
    && shelfOnlyShape?.Elements.Length == 1
    && shelfOnlyShape.Elements[0].Name == "shelf");

var leaningShelfShape = new Shape
{
    Elements =
    [
        new ShapeElement
        {
            Name = "origin",
            From = [8, 0, 8],
            To = [8, 0, 8],
            RotationOrigin = [8, 0, 8],
            Children =
            [
                new ShapeElement
                {
                    Name = "book 1",
                    From = [-5.5, 1.3, -4.5],
                    To = [-5.5, 1.3, -4.5],
                    RotationOrigin = [-3, 1, -4.5],
                    RotationY = 8,
                    Children = [new ShapeElement { Name = "pages" }]
                },
                new ShapeElement
                {
                    Name = "book 2",
                    From = [-0.9, 1.5, -3.8],
                    To = [-0.9, 1.5, -3.8],
                    RotationOrigin = [1.6, 1.2, -3.8],
                    RotationY = -20,
                    RotationZ = 23,
                    Children = [new ShapeElement { Name = "pages30" }]
                }
            ]
        }
    ]
};
var leaningBookPoses = ReadableClutterShelfPoses.Extract(leaningShelfShape);
var firstPivot = TransformPoint(leaningBookPoses[0], 0.5f, 0, 0.5f);
var secondPivot = TransformPoint(leaningBookPoses[1], 0.5f, 0, 0.5f);
Check("authored shelf poses retain both leaning books in traversal order",
    leaningBookPoses.Length == 2
    && Near(firstPivot, (5f / 16f, 1f / 16f, 3.5f / 16f))
    && Near(secondPivot, (9.6f / 16f, 1.2f / 16f, 4.2f / 16f)));
Check("authored shelf poses retain each book's independent yaw and lean",
    Math.Abs(leaningBookPoses[0][8]) > 0.1f
    && Math.Abs(leaningBookPoses[1][1]) > 0.3f
    && Math.Abs(leaningBookPoses[1][8]) > 0.3f);

var shelfPatch = ReadJson<JArray>("mod/assets/liberterra/patches/clutter-bookshelf-readable.json");
var shelfPatchTargets = new[]
{
    "game:blocktypes/wood/bookshelf-clutter.json",
    "game:blocktypes/wood/bookshelf-clutter-agedacacia.json",
    "game:blocktypes/wood/bookshelf-clutter-lore.json"
};
Check("normal, aged-acacia and lore clutter shelves receive all three runtime hooks",
    shelfPatch.Count == 9
    && shelfPatchTargets.All(file =>
        shelfPatch.Count(patch => patch!["file"]!.Value<string>() == file) == 3
        && new[] { "/class", "/entityClass", "/entityBehaviors/0/name" }.All(path =>
            shelfPatch.Any(patch =>
                patch!["file"]!.Value<string>() == file
                && patch["path"]!.Value<string>() == path))));
Check("the readable shelf keeps vanilla block behavior and uses a display inventory",
    typeof(BlockReadableClutterBookshelf).IsSubclassOf(typeof(BlockClutterBookshelf))
    && typeof(BlockReadableClutterBookshelfWithLore).IsSubclassOf(typeof(BlockClutterBookshelfWithLore))
    && typeof(BlockEntityReadableClutterBookshelf).IsSubclassOf(typeof(BlockEntityDisplay)));
Check("both shelf mesh behaviors extend their matching vanilla behavior",
    typeof(BEBehaviorReadableClutterBookshelf).IsSubclassOf(typeof(BEBehaviorClutterBookshelf))
    && typeof(BEBehaviorReadableClutterBookshelfWithLore)
        .IsSubclassOf(typeof(BEBehaviorClutterBookshelfWithLore)));
Check("the mod registers every readable shelf class and behavior",
    new[]
    {
        "RegisterBlockClass(\"ReadableClutterBookshelf\"",
        "\"ReadableClutterBookshelfWithLore\",",
        "RegisterBlockEntityClass(\n            \"ReadableClutterBookshelf\"",
        "typeof(BEBehaviorReadableClutterBookshelf)",
        "typeof(BEBehaviorReadableClutterBookshelfWithLore)"
    }.All(ReadText("mod/src/LiberTerraModSystem.cs").Contains));

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

(float X, float Y, float Z) TransformPoint(float[] matrix, float x, float y, float z) =>
    (matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
     matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
     matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14]);

bool Near((float X, float Y, float Z) actual, (float X, float Y, float Z) expected) =>
    Math.Abs(actual.X - expected.X) < 0.00001f
    && Math.Abs(actual.Y - expected.Y) < 0.00001f
    && Math.Abs(actual.Z - expected.Z) < 0.00001f;

T ReadJson<T>(string relativePath) where T : JToken => (T)JToken.Parse(ReadText(relativePath));

ItemStack BookStack(string code, bool editable)
{
    var item = new Item
    {
        Code = new AssetLocation("game", code),
        Attributes = new Vintagestory.API.Datastructures.JsonObject(
            JObject.Parse($$"""{ "editable": {{editable.ToString().ToLowerInvariant()}} }"""))
    };

    return new ItemStack(item);
}

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
