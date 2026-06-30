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
    // A settings record with every field set to a non-default value (a mix of
    // Pro and free fields). Clamping must drop the Pro values and preserve the
    // free ones (incl. Position, which is free since c7f8ce5).
    private static AppSettings NonDefaultSettings() => AppSettings.Default with
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
        var s = NonDefaultSettings();
        Assert.Same(s, s.ClampForEntitlement(isPro: true));
    }

    [Fact]
    public void NotPro_Preserves_Position()
    {
        // Position (Top/Bottom) is a FREE feature — clamping must not touch it.
        var clamped = NonDefaultSettings().ClampForEntitlement(isPro: false);
        Assert.Equal(BarPosition.Bottom, clamped.Position);
    }

    [Fact]
    public void NotPro_Clamps_GradientMode_ToFalse()
    {
        var clamped = NonDefaultSettings().ClampForEntitlement(isPro: false);
        Assert.False(clamped.GradientMode);
    }

    [Fact]
    public void NotPro_Clamps_Color_ToFreeColor()
    {
        var clamped = NonDefaultSettings().ClampForEntitlement(isPro: false);
        Assert.Equal(AppSettings.FreeColor, clamped.Color);
    }

    [Fact]
    public void NotPro_Clamps_AlwaysAbove_ToFalse()
    {
        var clamped = NonDefaultSettings().ClampForEntitlement(isPro: false);
        Assert.False(clamped.AlwaysAboveEverything);
    }

    [Fact]
    public void NotPro_Preserves_FreeFields()
    {
        // Free-tier fields must round-trip untouched: opacity, height, default
        // duration, completion sound, hide-list, presets, etc.
        var original = NonDefaultSettings();
        var clamped = original.ClampForEntitlement(isPro: false);
        Assert.Equal(original.Opacity, clamped.Opacity);
        Assert.Equal(original.Height, clamped.Height);
        Assert.Equal(original.DefaultDuration, clamped.DefaultDuration);
        Assert.Equal(original.PlayCompletionSound, clamped.PlayCompletionSound);
        Assert.Same(original.HideForProcesses, clamped.HideForProcesses);
    }

    [Fact]
    public void NotPro_DoesNotMutate_OriginalSettings()
    {
        // ClampForEntitlement returns a NEW record; the stored values on the
        // original survive so re-purchase / Restore restores the full Pro state.
        var original = NonDefaultSettings();
        var snapshot = original;
        _ = original.ClampForEntitlement(isPro: false);
        Assert.Equal(snapshot, original);
        Assert.Equal(BarPosition.Bottom, original.Position);
        Assert.True(original.GradientMode);
        Assert.Equal(BarColor.Purple, original.Color);
        Assert.True(original.AlwaysAboveEverything);
    }

    [Fact]
    public void NotPro_OnAlreadyFreeSettings_ReturnsEquivalent()
    {
        // No-op shape: a settings record already at free values stays equal.
        var free = AppSettings.Default;
        var clamped = free.ClampForEntitlement(isPro: false);
        Assert.Equal(free, clamped);
    }

    [Fact]
    public void RoundTrip_NotPro_ThenPro_RestoresAllProValues()
    {
        // The whole point of clamping (vs. mutating): a Pro user who refunds
        // and re-purchases gets back exactly what they had.
        var original = NonDefaultSettings();
        var clamped = original.ClampForEntitlement(isPro: false);
        // The stored record is untouched; clamping it with isPro=true is identity.
        var restored = original.ClampForEntitlement(isPro: true);
        Assert.Equal(original, restored);
        Assert.NotEqual(clamped, restored);
    }
}
