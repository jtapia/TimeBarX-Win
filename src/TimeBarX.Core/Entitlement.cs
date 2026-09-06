namespace TimeBarX.Core;

/// <summary>
/// The set of capabilities unlocked for the current user, passed to
/// <see cref="AppSettings.ClampForEntitlement(Entitlement)"/> and other gate
/// points. A value type carrying named flags rather than a bare <c>bool</c> so
/// new gating axes (e.g. multi-monitor) become a new property instead of an
/// extra positional argument that every call site must thread through.
/// </summary>
public readonly record struct Entitlement(bool Pro)
{
    /// <summary>Nothing unlocked — the free tier.</summary>
    public static readonly Entitlement Free = new(Pro: false);

    /// <summary>Everything unlocked — Pro (via Store, license key, or trial).</summary>
    public static readonly Entitlement ProUnlocked = new(Pro: true);

    /// <summary>Build from the single Pro flag the entitlement sources expose today.</summary>
    public static Entitlement FromIsPro(bool isPro) => isPro ? ProUnlocked : Free;
}
