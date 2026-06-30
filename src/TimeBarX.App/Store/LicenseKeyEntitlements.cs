using System;
using System.IO;
using TimeBarX.Core;

namespace TimeBarX.App.Store;

/// <summary>
/// Entitlement source backed by an offline license key, used by the direct
/// (Inno) channel where <see cref="StoreEntitlements"/> isn't available. The
/// key file lives next to the timer/settings state in %APPDATA%\TimeBarX\.
///
/// On the Store build this is composed alongside <see cref="StoreEntitlements"/>
/// (via <see cref="OrEntitlements"/>) so a user who originally bought direct
/// and later installs from the Store keeps Pro until they refund or wipe state.
/// </summary>
public sealed class LicenseKeyEntitlements : IEntitlements
{
    private readonly string _path;
    private bool _isPro;

    public LicenseKeyEntitlements(string? path = null)
    {
        _path = path ?? DefaultPath();
        _isPro = ReadAndVerify();
    }

    public bool IsPro => _isPro;

    public event Action? Changed;

    /// <summary>Default path: %APPDATA%\TimeBarX\license.txt</summary>
    public static string DefaultPath()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(dir, "TimeBarX", "license.txt");
    }

    /// <summary>
    /// Verify a pasted key and (on success) persist it. Returns <c>true</c> if
    /// the key was accepted; <c>false</c> on a bad/forged key (no state changes).
    /// Fires <see cref="Changed"/> only on a real transition.
    /// </summary>
    public bool Activate(string key)
    {
        if (!LicenseKey.TryVerify(key, out _)) return false;
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path, key.Trim());
        }
        catch
        {
            // Disk full / permission denied: still grant in-memory Pro for this
            // session so the user isn't stranded after a successful verify.
        }
        SetIsPro(true);
        return true;
    }

    /// <summary>Remove the stored key (refund, machine swap, "log out"). Idempotent.</summary>
    public void Deactivate()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch { /* best-effort */ }
        SetIsPro(false);
    }

    private bool ReadAndVerify()
    {
        try
        {
            if (!File.Exists(_path)) return false;
            var key = File.ReadAllText(_path);
            return LicenseKey.TryVerify(key, out _);
        }
        catch
        {
            return false;
        }
    }

    private void SetIsPro(bool value)
    {
        if (_isPro == value) return;
        _isPro = value;
        Changed?.Invoke();
    }
}

/// <summary>
/// Combines two entitlement sources: Pro if either source reports Pro. Used to
/// merge Store IAP and direct license-key entitlements so users who purchased
/// through one channel keep Pro after switching installation channels.
/// </summary>
public sealed class OrEntitlements : IEntitlements
{
    private readonly IEntitlements _a;
    private readonly IEntitlements _b;

    public OrEntitlements(IEntitlements a, IEntitlements b)
    {
        _a = a;
        _b = b;
        _a.Changed += Forward;
        _b.Changed += Forward;
    }

    public bool IsPro => _a.IsPro || _b.IsPro;

    public event Action? Changed;

    private void Forward() => Changed?.Invoke();
}
