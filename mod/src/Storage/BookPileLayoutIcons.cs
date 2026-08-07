using Cairo;
using Vintagestory.API.MathTools;

namespace LiberTerra.Storage;

/// <summary>
/// Tool-mode icons for the layout picker, each a side-on sketch of its pile: squared bars for
/// Neat, cocked ones for Messy, uprights for Shelved, and so on. Bars carry their own width so a
/// style built from narrow side-by-side stacks reads differently from one wide stack.
/// </summary>
public static class BookPileLayoutIcons
{
    private const double BarHeight = 0.125;
    private const double Wide = 0.62;
    private const double Half = 0.34;
    private const double Mid = 0.44;

    /// <summary>Offset from the icon's horizontal middle, baseline, tilt, and bar width.</summary>
    private static readonly (double dx, double y, double angle, double width)[] NeatBars =
    [
        (0.00, 0.760, 0, Wide),
        (0.00, 0.585, 0, Wide),
        (0.00, 0.410, 0, Wide),
        (0.00, 0.235, 0, Wide)
    ];

    private static readonly (double dx, double y, double angle, double width)[] MessyBars =
    [
        (0.030, 0.775, -5, Wide),
        (-0.045, 0.595, 6, Wide),
        (0.055, 0.415, -7, Wide),
        (-0.020, 0.235, 3, Wide)
    ];

    private static readonly (double dx, double y, double angle, double width)[] TumbledBars =
    [
        (0.055, 0.790, -9, Wide),
        (-0.070, 0.600, 11, Wide),
        (0.040, 0.410, -6, Wide),
        (0.150, 0.245, 38, Wide)
    ];

    /// <summary>Upright books seen end on, so these bars stand rather than lie.</summary>
    private static readonly (double dx, double y, double angle, double width)[] ShelvedBars =
    [
        (-0.240, 0.500, 90, Wide),
        (-0.080, 0.500, 90, Wide),
        (0.080, 0.500, 90, Wide),
        (0.240, 0.500, 90, Wide)
    ];

    /// <summary>
    /// A stack with a book propped against each side. Angles are clockwise on a y-down canvas, so
    /// the left book needs a negative tilt and the right a positive one for both to rest their
    /// tops against the stack rather than fall away from it.
    /// </summary>
    private static readonly (double dx, double y, double angle, double width)[] LeaningBars =
    [
        (0.000, 0.780, 0, Mid),
        (0.000, 0.635, 0, Mid),
        (0.000, 0.490, 0, Mid),
        (-0.250, 0.575, -66, Mid),
        (0.250, 0.575, 66, Mid)
    ];

    /// <summary>Columns of unequal height, with a book slanted across them.</summary>
    private static readonly (double dx, double y, double angle, double width)[] UnevenBars =
    [
        (-0.175, 0.815, 0, Half),
        (-0.175, 0.670, 0, Half),
        (-0.175, 0.525, 0, Half),
        (0.205, 0.815, 0, Half),
        (0.020, 0.395, 20, Wide)
    ];

    /// <summary>Two low stacks with a pair bridging the gap above them.</summary>
    private static readonly (double dx, double y, double angle, double width)[] BridgedBars =
    [
        (-0.215, 0.830, 0, Half),
        (0.215, 0.830, 0, Half),
        (-0.215, 0.685, 0, Half),
        (0.215, 0.685, 0, Half),
        (0.000, 0.540, 0, Wide)
    ];

    /// <summary>Spread low and wide, with one book slumped almost flat.</summary>
    private static readonly (double dx, double y, double angle, double width)[] ScatteredBars =
    [
        (-0.215, 0.830, -4, Half),
        (0.235, 0.830, 5, Half),
        (-0.145, 0.685, 3, Half),
        (0.265, 0.685, -6, Half),
        (-0.020, 0.540, 14, Mid)
    ];

    public static void DrawMessy(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, MessyBars);

    public static void DrawNeat(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, NeatBars);

    public static void DrawTumbled(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, TumbledBars);

    public static void DrawShelved(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, ShelvedBars);

    public static void DrawLeaning(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, LeaningBars);

    public static void DrawUneven(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, UnevenBars);

    public static void DrawBridged(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, BridgedBars);

    public static void DrawScattered(Context cr, int x, int y, float w, float h, double[] rgba) =>
        Draw(cr, x, y, w, h, rgba, ScatteredBars);

    private static void Draw(
        Context cr,
        int x,
        int y,
        float width,
        float height,
        double[] rgba,
        (double dx, double y, double angle, double width)[] bars)
    {
        cr.Save();
        cr.Translate(x, y);

        // Bars are filled rather than stroked so the rotation never distorts a line width.
        foreach (var (dx, baseline, angle, barWidth) in bars)
        {
            var bw = width * barWidth;
            var bh = height * BarHeight;

            cr.Save();
            cr.Translate(width * (0.5 + dx), height * baseline);
            cr.Rotate(angle * GameMath.DEG2RAD);
            cr.Rectangle(-bw / 2, -bh / 2, bw, bh);
            cr.SetSourceRGBA(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.Fill();
            cr.Restore();
        }

        cr.Restore();
    }
}
