using LiberTerra.Storage;
using FilePath = System.IO.Path;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

internal static class ClutterShelfAssets
{
    public static void CheckInstalledShapes(string survivalAssets, Action<string, bool, string> check)
    {
        var shapes = new Dictionary<string, string>();
        var markedLoreShapes = new HashSet<string>();
        foreach (var (file, behavior) in new[]
        {
            ("bookshelf-clutter", "ClutterBookshelf"),
            ("bookshelf-clutter-agedacacia", "ClutterBookshelf"),
            ("bookshelf-clutter-lore", "ClutterBookshelfWithLore")
        })
        {
            var block = JObject.Parse(File.ReadAllText(FilePath.Combine(survivalAssets, "blocktypes", "wood", file + ".json")));
            check($"{file} exposes the expected block, entity and behavior to the patch",
                (string?)block["class"] == "Block" + behavior
                && (string?)block["entityClass"] == "Generic"
                && (string?)block["entityBehaviors"]?[0]?["name"] == behavior, "");

            var attributes = block["attributes"]!;
            var shapeDirectory = FilePath.Combine(survivalAssets, "shapes", (string)attributes["shapeBasePath"]!);
            foreach (var codeToken in attributes.SelectTokens("variantGroups.*.types[*].code"))
            {
                var variant = (string)codeToken!;
                shapes[variant] = FilePath.Combine(shapeDirectory, variant + ".json");
                if (behavior == "ClutterBookshelfWithLore")
                {
                    markedLoreShapes.Add(variant);
                }
            }
            CheckFaceLookup(block, survivalAssets, check);
        }

        var readableCount = 0;
        foreach (var (code, file) in shapes.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var shape = ReadShape(file);
            var poses = ReadableClutterShelfPoses.Extract(shape);
            var readable = ReadableClutterShelfConversion.TryGetBookCount(code, out var count);
            check($"{code} matches the authored book positions", poses.Length == count
                && poses.All(matrix => matrix.All(float.IsFinite)), $"inventory {count}, shape {poses.Length}");
            if (!readable)
            {
                continue;
            }

            readableCount++;
            var furniture = FurnitureElements(shape.Elements).ToArray();
            var stripped = ReadableClutterShelfMesh.RemoveBakedBooks(shape)!;
            check($"{code} preserves furniture and removes its decorative books",
                furniture.Length > 0 && furniture.SequenceEqual(FurnitureElements(stripped.Elements))
                && ReadableClutterShelfPoses.Extract(stripped).Length == 0, "");
        }
        check("all supported readable shelf designs occur in the installed game", readableCount == 50,
            $"{readableCount} readable designs");

        foreach (var code in markedLoreShapes)
        {
            var marked = FilePath.ChangeExtension(shapes[code], null) + "-book.json";
            check($"{code} keeps its one separate discovery book",
                ReadableClutterShelfPoses.Extract(ReadShape(marked)).Length == 1
                && !ReadableClutterShelfConversion.TryGetBookCount(code + "-book", out _), "");
        }

        var canonical = ReadShape(FilePath.Combine(survivalAssets, "shapes", "block", "clutter", "bookshelves", "small-normal.json"));
        var canonicalPoses = ReadableClutterShelfPoses.Extract(canonical);
        var identity = Mat4f.Create();
        check("the installed book model agrees with the pose used to align shelf books",
            canonicalPoses.Length == 1
            && canonicalPoses[0].Zip(identity).All(pair => Math.Abs(pair.First - pair.Second) < 0.00001f), "");
    }

    private static void CheckFaceLookup(JObject definition, string survivalAssets, Action<string, bool, string> check)
    {
        var attributes = definition["attributes"]!;
        var block = new BlockClutterBookshelf
        {
            Code = new AssetLocation("game", (string)definition["code"]!),
            basePath = (string)attributes["shapeBasePath"]!
        };
        foreach (var entry in ((JObject)attributes["variantGroups"]!).Properties())
        {
            if ((bool?)entry.Value["doubleSided"] != true)
            {
                continue;
            }

            // Match the design table vanilla builds in LoadTypes, before collecting textures.
            var group = entry.Value.ToObject<BookShelfVariantGroup>()!;
            group.block = block;
            block.variantGroupsByCode[entry.Name] = group;
            foreach (var design in group.types)
            {
                design.Variant = entry.Name;
                design.group = group;
                group.typesByCode[design.Code] = design;
            }
            ReadableClutterShelfConversion.BindDoubleSidedShapePaths([group]);
            check($"{block.Code} {entry.Name} binds every design before vanilla collects its shape",
                group.types.All(design => design.Type1 == design.Code), "");

            // Load the two actual files chosen by ShapePath, then exercise the game's paired lookup.
            var first = group.types.First();
            var second = group.types.Last();
            first.ShapeResolved = ReadShape(FilePath.Combine(survivalAssets, first.ShapePath.Path));
            second.ShapeResolved = ReadShape(FilePath.Combine(survivalAssets, second.ShapePath.Path));
            var stack = new ItemStack(block);
            stack.Attributes.SetString("variant", entry.Name);
            stack.Attributes.SetString("type1", first.Code);
            stack.Attributes.SetString("type2", second.Code);
            var pair = (BookShelfTypeProps)block.GetTypeProps(first.Code, stack, null!);
            ReadableClutterShelfConversion.TryGetBookCount(first.Code, out var firstCount);
            ReadableClutterShelfConversion.TryGetBookCount(second.Code, out var secondCount);
            check($"{block.Code} {entry.Name} resolves both saved shelf faces",
                pair.ShapePath.Equals(first.ShapePath) && pair.ShapePath2.Equals(second.ShapePath)
                && ReadableClutterShelfPoses.Extract(pair.ShapeResolved).Length == firstCount
                && ReadableClutterShelfPoses.Extract(pair.ShapeResolved2).Length == secondCount,
                $"{first.Code} / {second.Code}");
        }
    }

    private static Shape ReadShape(string file) => JsonConvert.DeserializeObject<Shape>(File.ReadAllText(file))!;

    private static IEnumerable<string?> FurnitureElements(IEnumerable<ShapeElement> elements)
    {
        foreach (var element in elements.Where(element => !ReadableClutterShelfPoses.IsModeledBookRoot(element)))
        {
            yield return element.Name;
            foreach (var child in FurnitureElements(element.Children ?? []))
            {
                yield return child;
            }
        }
    }
}
