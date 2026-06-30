namespace TimeBarX.Core;

/// <summary>
/// Single source of truth for whether the user has unlocked Pro. The rest of
/// the app reads <see cref="IsPro"/> to decide whether to apply Pro-gated
/// settings; UI surfaces Pro features unconditionally and uses this flag to
/// route locked clicks into the upgrade flow.
///
/// Implementations live in the platform layer (the Store implementation queries
/// <c>Windows.Services.Store</c>; tests/dev use a mock). The Core layer takes
/// only the abstraction so settings clamping and tests stay platform-agnostic.
/// </summary>
public interface IEntitlements
{
    /// <summary>True iff Pro features should be available.</summary>
    bool IsPro { get; }

    /// <summary>Fires when <see cref="IsPro"/> transitions (purchase, refund, restore).</summary>
    event Action? Changed;
}

/// <summary>
/// Always-free entitlement. Used as the safe default when no platform impl is
/// available (e.g. macOS dev builds, headless tests). Never raises Changed.
/// </summary>
public sealed class FreeEntitlements : IEntitlements
{
    public bool IsPro => false;
    public event Action? Changed { add { } remove { } }
}
