using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class EntitlementsTests
{
    [Fact]
    public void FreeEntitlements_IsPro_IsFalse()
    {
        var ent = new FreeEntitlements();
        Assert.False(ent.IsPro);
    }

    [Fact]
    public void FreeEntitlements_Changed_DoesNotThrowOnSubscribeUnsubscribe()
    {
        var ent = new FreeEntitlements();
        Action handler = () => { };
        ent.Changed += handler;
        ent.Changed -= handler;
        // The point: FreeEntitlements.Changed is a no-op add/remove, must not NRE.
        Assert.False(ent.IsPro);
    }
}

public class ClampForEntitlementTests
{
    private static AppSettings ProSettings() => AppSettings.Default with
    {
        Color = BarColor.Purple,
        GradientMode = true,
        AlwaysAboveEverything = true,
        Position = BarPosition.Bottom,
        Opacity = 0.6,
    };

    [Fact]
    public void IsPro_Returns_SameInstance()
    {
        var s = ProSettings();
        Assert.Same(s, s.ClampForEntitlement(isPro: true));
    }

    [Fact]
    public void NotPro_Clamps_Position_ToTop()
    {
        var clamped = ProSettings().ClampForEntitlement(isPro: false);
        Assert.Equal(BarPosition.Top, clamped.Position);
    }

    [Fact]
    public void NotPro_Clamps_GradientMode_ToFalse()
    {
        var clamped = ProSettings().ClampForEntitlement(isPro: false);
        Assert.False(clamped.GradientMode);
    }

    [Fact]
    public void NotPro_Clamps_Color_ToFreeColor()
    {
        var clamped = ProSettings().ClampForEntitlement(isPro: false);
        Assert.Equal(AppSettings.FreeColor, clamped.Color);
    }

    [Fact]
    public void NotPro_Clamps_AlwaysAbove_ToFalse()
    {
        var clamped = ProSettings().ClampForEntitlement(isPro: false);
        Assert.False(clamped.AlwaysAboveEverything);
    }

    [Fact]
    public void NotPro_Preserves_FreeFields()
    {
        // Free-tier fields must round-trip untouched: opacity, height, default
        // duration, completion sound, hide-list, presets, etc.
        var pro = ProSettings();
        var clamped = pro.ClampForEntitlement(isPro: false);
        Assert.Equal(pro.Opacity, clamped.Opacity);
        Assert.Equal(pro.Height, clamped.Height);
        Assert.Equal(pro.DefaultDuration, clamped.DefaultDuration);
        Assert.Equal(pro.PlayCompletionSound, clamped.PlayCompletionSound);
        Assert.Same(pro.HideForProcesses, clamped.HideForProcesses);
    }

    [Fact]
    public void NotPro_DoesNotMutate_OriginalSettings()
    {
        // ClampForEntitlement returns a NEW record; the stored values on the
        // original survive so re-purchase / Restore restores the full Pro state.
        var pro = ProSettings();
        var snapshot = pro;
        _ = pro.ClampForEntitlement(isPro: false);
        Assert.Equal(snapshot, pro);
        Assert.Equal(BarPosition.Bottom, pro.Position);
        Assert.True(pro.GradientMode);
        Assert.Equal(BarColor.Purple, pro.Color);
        Assert.True(pro.AlwaysAboveEverything);
    }

    [Fact]
    public void NotPro_OnAlreadyFreeSettings_ReturnsEquivalent()
    {
        // No-op shape: a settings record already at free values stays equal.
        var free = AppSettings.Default;
        var clamped = free.ClampForEntitlement(isPro: false);
        Assert.Equal(free, clamped);
    }
}
