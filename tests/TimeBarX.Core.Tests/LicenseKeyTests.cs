using TimeBarX.Core;
using Xunit;

namespace TimeBarX.Core.Tests;

public class LicenseKeyTests
{
    private const string TestSecret = "test-secret-do-not-ship";

    [Fact]
    public void Issue_Then_Verify_RoundTrips_Payload()
    {
        var key = LicenseKey.Issue("user@example.com", TestSecret);
        Assert.True(LicenseKey.TryVerify(key, out var payload, TestSecret));
        Assert.Equal("user@example.com", payload);
    }

    [Fact]
    public void Issue_Produces_TBX1_PrefixedKey()
    {
        var key = LicenseKey.Issue("alice", TestSecret);
        Assert.StartsWith("TBX1-", key);
        Assert.Equal(3, key.Split('-').Length);
    }

    [Fact]
    public void Verify_RejectsNull_EmptyAndWhitespace()
    {
        Assert.False(LicenseKey.TryVerify(null, out _, TestSecret));
        Assert.False(LicenseKey.TryVerify("", out _, TestSecret));
        Assert.False(LicenseKey.TryVerify("   ", out _, TestSecret));
    }

    [Fact]
    public void Verify_RejectsMissingPrefix()
    {
        // Strip the TBX1 prefix → malformed.
        var good = LicenseKey.Issue("alice", TestSecret);
        var bad = good.Substring(5); // drop "TBX1-"
        Assert.False(LicenseKey.TryVerify(bad, out _, TestSecret));
    }

    [Fact]
    public void Verify_RejectsWrongSecret()
    {
        var key = LicenseKey.Issue("alice", TestSecret);
        Assert.False(LicenseKey.TryVerify(key, out _, secret: "wrong-secret"));
    }

    [Fact]
    public void Verify_RejectsTamperedPayload()
    {
        // Splice in an unrelated payload while keeping the original signature.
        var keyA = LicenseKey.Issue("alice", TestSecret);
        var keyB = LicenseKey.Issue("eve",   TestSecret);
        var partsA = keyA.Split('-');
        var partsB = keyB.Split('-');
        var spliced = $"{partsA[0]}-{partsB[1]}-{partsA[2]}"; // eve payload + alice sig
        Assert.False(LicenseKey.TryVerify(spliced, out _, TestSecret));
    }

    [Fact]
    public void Verify_RejectsBitFlippedSignature()
    {
        var key = LicenseKey.Issue("alice", TestSecret);
        var parts = key.Split('-');
        // Flip one character of the sig (base32: cycle A→B).
        var sig = parts[2].ToCharArray();
        sig[0] = sig[0] == 'A' ? 'B' : 'A';
        var tampered = $"{parts[0]}-{parts[1]}-{new string(sig)}";
        Assert.False(LicenseKey.TryVerify(tampered, out _, TestSecret));
    }

    [Fact]
    public void Verify_RejectsExtraSegments()
    {
        var key = LicenseKey.Issue("alice", TestSecret);
        Assert.False(LicenseKey.TryVerify(key + "-extra", out _, TestSecret));
    }

    [Fact]
    public void Verify_AcceptsLeadingAndTrailingWhitespace()
    {
        // Users paste from email; tolerate cruft.
        var key = LicenseKey.Issue("alice", TestSecret);
        Assert.True(LicenseKey.TryVerify("  " + key + "  ", out _, TestSecret));
        Assert.True(LicenseKey.TryVerify("\"" + key + "\"", out _, TestSecret));
    }

    [Fact]
    public void Verify_IsCaseInsensitive_ForBase32()
    {
        var key = LicenseKey.Issue("alice", TestSecret);
        var lower = key.ToLowerInvariant();
        // Prefix uppercase preserved by Issue, but the base32 segments should
        // decode either case (we ToUpperInvariant inside the decoder).
        var lowerWithPrefix = "TBX1-" + lower.Substring(5);
        Assert.True(LicenseKey.TryVerify(lowerWithPrefix, out _, TestSecret));
    }

    [Fact]
    public void Issue_ProducesStableOutputForStableInput()
    {
        var k1 = LicenseKey.Issue("alice", TestSecret);
        var k2 = LicenseKey.Issue("alice", TestSecret);
        Assert.Equal(k1, k2);
    }
}
