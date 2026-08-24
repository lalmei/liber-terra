using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace LiberTerra.Storage;

/// <summary>
/// Describes the actual book geometry baked into vanilla's clutter shelves. These are a different
/// block family from standalone bookrows: the shelf, other clutter, and decorative books can share
/// one shape, so everything except the book elements must stay while real items replace them.
/// </summary>
internal static class ReadableClutterShelfConversion
{
    private const string RuinedShelfPrefix = "bookshelves/bookshelf-ruined-full";
    private const string LoreShelfPrefix = "bookshelves/bookshelf-ruined-full-lore";
    private const string StandardShelfPrefix = "bookshelves/bookshelf-standard";
    private const string FancyShelfPrefix = "bookshelves/bookshelf-fancy";
    private const string StuffShelfPrefix = "bookshelves/bookshelf-stuff";

    private static readonly int[] RuinedShelfBookCounts =
        [6, 2, 1, 8, 3, 3, 10, 7, 8, 14, 13, 11, 10, 5, 8, 11, 11];

    private static readonly int[] LoreShelfBookCounts = [5, 7, 9, 7, 3, 2, 1];
    private static readonly int[] StandardShelfBookCounts = [14, 14, 14, 6, 11, 11, 5, 7, 12];
    private static readonly int[] FancyShelfBookCounts = [14, 11, 12, 12, 9];
    private static readonly int[] StuffShelfBookCounts = [11, 12, 12, 2, 5, 6, 4, 5, 8, 3, 8];

    private static readonly IReadOnlyDictionary<string, int> ShelfBookCounts = BuildBookCounts();

    public static bool TryGetBookCount(string? shelfType, out int bookCount)
    {
        if (string.IsNullOrWhiteSpace(shelfType))
        {
            bookCount = 0;
            return false;
        }

        return ShelfBookCounts.TryGetValue(shelfType, out bookCount);
    }

    private static IReadOnlyDictionary<string, int> BuildBookCounts()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["bookshelves/bookshelf-full"] = 14
        };

        AddNumbered(counts, StandardShelfPrefix, StandardShelfBookCounts);
        AddNumbered(counts, FancyShelfPrefix, FancyShelfBookCounts);
        AddNumbered(counts, StuffShelfPrefix, StuffShelfBookCounts, padToTwoDigits: true);
        AddNumbered(counts, RuinedShelfPrefix, RuinedShelfBookCounts);
        AddNumbered(counts, LoreShelfPrefix, LoreShelfBookCounts);

        return counts;
    }

    private static void AddNumbered(
        IDictionary<string, int> destination,
        string prefix,
        IReadOnlyList<int> counts,
        bool padToTwoDigits = false)
    {
        for (var index = 0; index < counts.Count; index++)
        {
            var number = padToTwoDigits ? $"{index + 1:00}" : (index + 1).ToString();
            destination.Add(prefix + number, counts[index]);
        }
    }
}

/// <summary>Builds a shelf-only mesh by removing roots that are recognizably modeled books.</summary>
internal static class ReadableClutterShelfMesh
{
    public static IShapeTypeProps WithoutBakedBooks(IShapeTypeProps original)
    {
        if (original is not BookShelfTypeProps props)
        {
            return original;
        }

        var doubleSided = props.group.DoubleSided;
        var firstType = doubleSided ? props.Type1 : props.Code;
        var stripFirst = ReadableClutterShelfConversion.TryGetBookCount(firstType, out _);
        var stripSecond = doubleSided
                          && ReadableClutterShelfConversion.TryGetBookCount(props.Type2, out _);
        if (!stripFirst && !stripSecond)
        {
            return original;
        }

        // Preserve real shape paths while making the vanilla mesh-cache key unique. For a
        // one-sided group ShapePath uses Code, so Type1 is safe cache salt; double-sided groups use
        // Type1/Type2 as paths, so Code is the safe salt instead.
        var replacement = new BookShelfTypeProps
        {
            group = props.group,
            Code = doubleSided ? props.Code + "-liberterra-readable" : props.Code,
            Type1 = doubleSided ? props.Type1 : (props.Type1 ?? "") + "-liberterra-readable",
            Type2 = props.Type2,
            Variant = props.Variant,
            ShapeResolved = stripFirst ? RemoveBakedBooks(props.ShapeResolved) : props.ShapeResolved,
            ShapeResolved2 = stripSecond ? RemoveBakedBooks(props.ShapeResolved2) : props.ShapeResolved2,
            // A generic LOD shelf would put decorative books back at distance. Reuse the stripped
            // full mesh for both calls instead.
            ShapeLOD2Resolved = null,
            TexPos = props.TexPos,
            Material = props.Material,
            FirstTexture = props.FirstTexture,
            TextureFlipCode = props.TextureFlipCode,
            TextureFlipGroupCode = props.TextureFlipGroupCode,
            LightHsv = props.LightHsv
        };

        return replacement;
    }

    public static Shape? RemoveBakedBooks(Shape? original)
    {
        if (original?.Elements is null)
        {
            return original;
        }

        var shape = original.Clone();
        var bookRoots = new List<string>();
        CollectBookRoots(shape.Elements, bookRoots);
        shape.RemoveElements(bookRoots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        return shape;
    }

    private static void CollectBookRoots(IEnumerable<ShapeElement> elements, ICollection<string> roots)
    {
        foreach (var element in elements)
        {
            if (ReadableClutterShelfPoses.IsModeledBookRoot(element))
            {
                if (!string.IsNullOrWhiteSpace(element.Name))
                {
                    roots.Add(element.Name);
                }

                continue;
            }

            if (element.Children is { Length: > 0 } nested)
            {
                CollectBookRoots(nested, roots);
            }
        }
    }
}

/// <summary>
/// Maps the canonical real-book item mesh onto every modeled book root in a vanilla shelf shape.
/// ShapeElement supplies the same hierarchical pivot/rotation calculation used by the game's
/// tesselator, so leaning and irregular arrangements retain their authored pose exactly.
/// </summary>
internal static class ReadableClutterShelfPoses
{
    private static readonly float[] CanonicalBookInverse = CreateCanonicalBookInverse();

    public static float[][] Extract(Shape? shape)
    {
        if (shape?.Elements is null)
        {
            return [];
        }

        var poses = new List<float[]>();
        Collect(shape.Elements, Mat4f.Create(), poses);
        return poses.ToArray();
    }

    public static bool IsModeledBookRoot(ShapeElement element)
    {
        return element.Children is { Length: > 0 } children
               && children.Any(child =>
                   child.Name?.StartsWith("pages", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static void Collect(
        IEnumerable<ShapeElement> elements,
        float[] parentMatrix,
        ICollection<float[]> poses)
    {
        foreach (var element in elements)
        {
            // GetLocalTransformMatrix mutates the supplied matrix, so siblings each need a clone
            // of their parent's completed transform.
            var elementMatrix = element.GetLocalTransformMatrix(
                0,
                (float[])parentMatrix.Clone(),
                null!);

            if (IsModeledBookRoot(element))
            {
                // The inventory item uses game:block/clutter/bookshelves/small-normal. Its book
                // root has the same local geometry as these shelf books, but a canonical origin.
                // authored * inverse(canonical) therefore moves the real item onto this root while
                // preserving every parent transform, pivot, rotation, and scale.
                poses.Add(Mat4f.Mul(Mat4f.Create(), elementMatrix, CanonicalBookInverse));
                continue;
            }

            if (element.Children is { Length: > 0 } children)
            {
                Collect(children, elementMatrix, poses);
            }
        }
    }

    private static float[] CreateCanonicalBookInverse()
    {
        var canonicalRoot = new ShapeElement
        {
            Name = "book 1",
            From = [5.5, 0.3, 8],
            To = [5.5, 0.3, 8],
            RotationOrigin = [8, 0, 8]
        };
        var canonical = canonicalRoot.GetLocalTransformMatrix(0, Mat4f.Create(), null!);
        return Mat4f.Invert(Mat4f.Create(), canonical);
    }
}
