using System;
using TimeBarX.Core;

namespace TimeBarX.App.Store;

/// <summary>
/// Dev/test entitlement source. Reads <c>TIMEBARX_PRO</c> from the environment at
/// construction (truthy = "1", "true", "yes", case-insensitive) and exposes a
/// <see cref="SetPro"/> hatch for manual flips during testing. Used on non-Windows
/// builds and anywhere the real Store implementation isn't wired in yet.
/// Never use this in a shipped Store build.
/// </summary>
public sealed class MockEntitlements : IEntitlements
{
    private bool _isPro;

    public MockEntitlements()
    {
        _isPro = TruthyEnv("TIMEBARX_PRO");
    }

    public MockEntitlements(bool isPro)
    {
        _isPro = isPro;
    }

    public bool IsPro => _isPro;

    public event Action? Changed;

    /// <summary>Test-only: flip the entitlement and notify subscribers.</summary>
    public void SetPro(bool isPro)
    {
        if (_isPro == isPro) return;
        _isPro = isPro;
        Changed?.Invoke();
    }

    private static bool TruthyEnv(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(v)) return false;
        return v.Equals("1", StringComparison.OrdinalIgnoreCase)
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
