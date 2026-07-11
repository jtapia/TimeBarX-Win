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
    BarPosition Position = BarPosition.Top,
    IReadOnlyList<CustomPreset>? CustomPresets = null,
    CompletionSoundChoice? CompletionSound = null,
    int? AutoPauseOnIdleMinutes = null,
    PomodoroSettings? Pomodoro = null,
    bool RecordSessionHistory = true)
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
        Position: BarPosition.Top,
        CustomPresets: Array.Empty<CustomPreset>(),
        CompletionSound: null,
        AutoPauseOnIdleMinutes: null,
        Pomodoro: null,
        RecordSessionHistory: true);

    /// <summary>
    /// The completion sound to actually play, folding legacy settings files
    /// into the new enum: when <see cref="CompletionSound"/> is unset (older
    /// settings.json), we honor the boolean <see cref="PlayCompletionSound"/>
    /// so upgrading users don't silently lose their sound.
    /// </summary>
    public CompletionSoundChoice EffectiveCompletionSound => CompletionSound
        ?? (PlayCompletionSound ? CompletionSoundChoice.Asterisk : CompletionSoundChoice.Off);

    public AppSettings WithOpacity(double opacity)
    {
        if (opacity < 0) opacity = 0;
        if (opacity > 1) opacity = 1;
        return this with { Opacity = opacity };
    }

    /// <summary>
    /// Returns a copy with out-of-range or undefined values coerced back to sane
    /// defaults. Deserialization sets properties directly and bypasses the
    /// validation in the constructors/helpers, so a hand-edited or corrupt file
    /// (e.g. Opacity 42.0, or an enum value from another version) would otherwise
    /// load unclamped. Applied per-field so one bad value doesn't discard the rest.
    /// </summary>
    public AppSettings Sanitize()
    {
        var opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, 0.0, 1.0) : Default.Opacity;
        var color = Enum.IsDefined(Color) ? Color : Default.Color;
        var height = Enum.IsDefined(Height) ? Height : Default.Height;
        var position = Enum.IsDefined(Position) ? Position : Default.Position;
        var duration = DefaultDuration > TimeSpan.Zero ? DefaultDuration : Default.DefaultDuration;

        var sound = CompletionSound is { } cs && Enum.IsDefined(cs) ? cs : (CompletionSoundChoice?)null;
        var idle = AutoPauseOnIdleMinutes is > 0 and <= 240 ? AutoPauseOnIdleMinutes : null;
        var pomodoro = Pomodoro?.Sanitize();

        return this with
        {
            Opacity = opacity,
            Color = color,
            Height = height,
            Position = position,
            DefaultDuration = duration,
            HideForProcesses = HideForProcesses ?? DefaultHideList,
            CustomPresets = CustomPresets ?? Array.Empty<CustomPreset>(),
            CompletionSound = sound,
            AutoPauseOnIdleMinutes = idle,
            Pomodoro = pomodoro,
        };
    }

    /// <summary>The default bar color used when entitlement clamps non-Pro users.</summary>
    public const BarColor FreeColor = BarColor.Blue;

    /// <summary>
    /// Returns a view of this settings record safe to apply for the given
    /// entitlement. Pro-only values fall back to free behavior; the *stored*
    /// values on <c>this</c> are not mutated, so re-purchase / Restore restores
    /// the full Pro state without the user re-entering anything.
    ///
    /// Free-tier overrides (Position is NOT clamped — Top/Bottom is free):
    /// <list type="bullet">
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
            GradientMode = false,
            Color = FreeColor,
            AlwaysAboveEverything = false,
        };
    }
}
