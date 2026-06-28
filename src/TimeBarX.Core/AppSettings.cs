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
    Thin = 8,
    Normal = 10,
    Thick = 13,
}

public enum BarPosition
{
    Top,
    Bottom,
}

public sealed record AppSettings(
    BarColor Color,
    BarHeight Height,
    double Opacity,
    bool GradientMode,
    bool PlayCompletionSound,
    TimeSpan DefaultDuration,
    bool AlwaysAboveEverything = false,
    IReadOnlyList<string>? HideForProcesses = null,
    BarPosition Position = BarPosition.Top)
{
    public static IReadOnlyList<string> DefaultHideList { get; } = new[]
    {
        "vlc", "mpv", "wmplayer", "PotPlayerMini64", "PotPlayer", "mpc-hc64", "mpc-hc",
        "Netflix", "Disney+", "Hulu",
    };

    public static AppSettings Default => new(
        Color: BarColor.Blue,
        Height: BarHeight.Normal,
        Opacity: 1.0,
        GradientMode: false,
        PlayCompletionSound: false,
        DefaultDuration: TimeSpan.FromMinutes(25),
        AlwaysAboveEverything: false,
        HideForProcesses: DefaultHideList);

    public AppSettings WithOpacity(double opacity)
    {
        if (opacity < 0) opacity = 0;
        if (opacity > 1) opacity = 1;
        return this with { Opacity = opacity };
    }
}
