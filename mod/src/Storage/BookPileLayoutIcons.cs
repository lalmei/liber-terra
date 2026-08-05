using Cairo;
using Vintagestory.API.MathTools;

namespace LiberTerra.Storage;

/// <summary>
/// Tool-mode icons for the layout picker, drawn as a side-on stack of books:
/// squared bars for Neat, cocked ones for Messy, and one sliding off for Tumbled.
/// </summary>
public static class BookPileLayoutIcons
{
    private const double BarWidth = 0.62;
    private const double BarHeight = 0.125;

    /// <summary>Bar centre offset from the icon's horizontal middle, its baseline, and its tilt.</summary>
    private static readonly (double dx, double y, double angle)[] NeatBars =
    [
        (0.00, 0.760, 0),
        (0.00, 0.585, 0),
        (0.00, 0.410, 0),
        (0.00, 0.235, 0)
    ];

    private static readonly (double dx, double y, double angle)[] MessyBars =
    [
        (0.030, 0.775, -5),
        (-0.045, 0.595, 6),
        (0.055, 0.415, -7),
        (-0.020, 0.235, 3)
    ];

    private static readonly (double dx, double y, double angle)[] TumbledBars =
    [
        (0.055, 0.790, -9),
        (-0.070, 0.600, 11),
        (0.040, 0.410, -6),
        (0.150, 0.245, 38)
    ];

    /// <summary>Upright books seen end on, so these bars stand rather than lie.</summary>
    private static readonly (double dx, double y, double angle)[] ShelvedBars =
    [
        (-0.240, 0.500, 90),
        (-0.080, 0.500, 90),
        (0.080, 0.500, 90),
        (0.240, 0.500, 90)
    ];

    /// <summary>A stack with two books propped against it, one per side.</summary>
    private static readonly (double dx, double y, double angle)[] LeaningBars =
    [
        (0.000, 0.760, 0),
        (0.000, 0.605, 0),
        (0.000, 0.450, 0),
        (-0.280, 0.545, 66),
        (0.280, 0.545, -66)
    ];

    public static void DrawNeat(Context cr, int x, int y, float width, float height, double[] rgba)
    {
        Draw(cr, x, y, width, height, rgba, NeatBars);
    }

    public static void DrawShelved(Context cr, int x, int y, float width, float height, double[] rgba)
    {
        Draw(cr, x, y, width, height, rgba, ShelvedBars);
    }

    public static void DrawLeaning(Context cr, int x, int y, float width, float height, double[] rgba)
    {
        Draw(cr, x, y, width, height, rgba, LeaningBars);
    }

    public static void DrawMessy(Context cr, int x, int y, float width, float height, double[] rgba)
    {
        Draw(cr, x, y, width, height, rgba, MessyBars);
    }

    public static void DrawTumbled(Context cr, int x, int y, float width, float height, double[] rgba)
    {
        Draw(cr, x, y, width, height, rgba, TumbledBars);
    }

    private static void Draw(
        Context cr,
        int x,
        int y,
        float width,
        float height,
        double[] rgba,
        (double dx, double y, double angle)[] bars)
    {
        cr.Save();
        cr.Translate(x, y);

        // Bars are filled rather than stroked so the rotation never distorts a line width.
        var barWidth = width * BarWidth;
        var barHeight = height * BarHeight;

        foreach (var (dx, baseline, angle) in bars)
        {
            cr.Save();
            cr.Translate(width * (0.5 + dx), height * baseline);
            cr.Rotate(angle * GameMath.DEG2RAD);
            cr.Rectangle(-barWidth / 2, -barHeight / 2, barWidth, barHeight);
            cr.SetSourceRGBA(rgba[0], rgba[1], rgba[2], rgba[3]);
            cr.Fill();
            cr.Restore();
        }

        cr.Restore();
    }
}
