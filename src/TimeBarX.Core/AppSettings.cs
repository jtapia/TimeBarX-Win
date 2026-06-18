namespace TimeBarX.Core;

public enum BarColor
{
    Accent,
    Blue,
    Purple,
    Green,
    Red,
}

public enum BarHeight
{
    Thin = 2,
    Normal = 3,
    Thick = 4,
}

public sealed record AppSettings(
    BarColor Color,
    BarHeight Height,
    double Opacity,
    bool GradientMode,
    bool PlayCompletionSound,
    TimeSpan DefaultDuration)
{
    public static AppSettings Default => new(
        Color: BarColor.Blue,
        Height: BarHeight.Normal,
        Opacity: 1.0,
        GradientMode: false,
        PlayCompletionSound: false,
        DefaultDuration: TimeSpan.FromMinutes(25));

    public AppSettings WithOpacity(double opacity)
    {
        if (opacity < 0) opacity = 0;
        if (opacity > 1) opacity = 1;
        return this with { Opacity = opacity };
    }
}
