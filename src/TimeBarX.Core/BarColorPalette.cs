namespace TimeBarX.Core;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static Rgb Lerp(Rgb a, Rgb b, double t)
    {
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        return new Rgb(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}

public static class BarColorPalette
{
    // Preset solid colors. "Accent" defaults to Blue when no OS accent is known —
    // resolving the actual OS accent is a UI-layer concern.
    private static readonly Rgb Blue = new(0x3B, 0x82, 0xF6);
    private static readonly Rgb Purple = new(0x8B, 0x5C, 0xF6);
    private static readonly Rgb Green = new(0x10, 0xB9, 0x81);
    private static readonly Rgb Red = new(0xEF, 0x44, 0x44);
    private static readonly Rgb Yellow = new(0xF5, 0x9E, 0x0B);

    public static Rgb Solid(BarColor color) => color switch
    {
        BarColor.Accent => Blue,
        BarColor.Blue => Blue,
        BarColor.Purple => Purple,
        BarColor.Green => Green,
        BarColor.Red => Red,
        _ => Blue,
    };

    /// <summary>
    /// Green → Yellow → Red gradient driven by progress (0..1).
    /// </summary>
    public static Rgb Gradient(double progress)
    {
        if (progress < 0) progress = 0;
        if (progress > 1) progress = 1;

        return progress < 0.5
            ? Rgb.Lerp(Green, Yellow, progress * 2.0)
            : Rgb.Lerp(Yellow, Red, (progress - 0.5) * 2.0);
    }

    public static Rgb ForProgress(AppSettings settings, double progress)
        => settings.GradientMode ? Gradient(progress) : Solid(settings.Color);
}
