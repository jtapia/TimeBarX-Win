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
        HideForProcesses: DefaultHideList,
        Position: BarPosition.Top);

    public AppSettings WithOpacity(double opacity)
    {
        if (opacity < 0) opacity = 0;
        if (opacity > 1) opacity = 1;
        return this with { Opacity = opacity };
    }

    /// <summary>The default bar color used when entitlement clamps non-Pro users.</summary>
    public const BarColor FreeColor = BarColor.Blue;

    /// <summary>
    /// Returns a view of this settings record safe to apply for the given
    /// entitlement. Pro-only values fall back to free behavior; the *stored*
    /// values on <c>this</c> are not mutated, so re-purchase / Restore restores
    /// the full Pro state without the user re-entering anything.
    ///
    /// Free-tier overrides:
    /// <list type="bullet">
    ///   <item><see cref="Position"/> = <see cref="BarPosition.Top"/></item>
    ///   <item><see cref="GradientMode"/> = <c>false</c></item>
    ///   <item><see cref="Color"/> = <see cref="FreeColor"/> when not the default</item>
    ///   <item><see cref="AlwaysAboveEverything"/> = <c>false</c></item>
    /// </list>
    /// </summary>
    public AppSettings ClampForEntitlement(bool isPro)
    {
        if (isPro) return this;
        return this with
        {
            Position = BarPosition.Top,
            GradientMode = false,
            Color = FreeColor,
            AlwaysAboveEverything = false,
        };
    }
}
