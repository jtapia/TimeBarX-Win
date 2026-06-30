using System.Security.Cryptography;
using System.Text;

namespace TimeBarX.Core;

/// <summary>
/// Offline-verifiable license key for the direct (non-Store) Pro unlock.
/// Format: <c>TBX1-{payload}-{sig}</c>, where both components are RFC 4648
/// base32 (no padding, uppercase A-Z2-7). The signature is the first 10 bytes
/// of HMAC-SHA256(payload, secret) — a 50-bit MAC, which is more than enough
/// for a $5 desktop utility (an attacker would need ~2^50 trials per forgery
/// AND a copy of the redistributed cracked key works the same as not paying).
///
/// The signing secret is a build-time constant baked into the app. It is NOT a
/// crypto secret in the usual sense: a determined attacker who pulls the EXE
/// apart will find it. That's fine — license-key DRM on a $5 indie tool exists
/// to deter casual sharing and remind honest people to pay, not to be
/// unbreakable. Use a fresh secret per major version if you ever rotate.
///
/// The payload is opaque to the verifier — typically the buyer's email or an
/// order ID embedded by the issuer (Gumroad webhook → tiny static generator),
/// kept short so the key stays manageable to type.
/// </summary>
public static class LicenseKey
{
    private const string Prefix = "TBX1";
    private const int SigBytes = 10;

    /// <summary>
    /// Default signing secret. Replace at build time by setting MSBuild
    /// constant <c>TIMEBARX_LICENSE_SECRET</c>, or override in tests via the
    /// secret parameter on <see cref="TryVerify"/> / <see cref="Issue"/>.
    /// </summary>
    public const string DefaultSecret = "timebarx-direct-channel-v1";

    /// <summary>
    /// Verifies a license key against the secret. Returns <c>true</c> and the
    /// embedded payload on success; <c>false</c> on any malformed input or
    /// signature mismatch. Constant-time on the signature comparison.
    /// </summary>
    public static bool TryVerify(string? key, out string payload, string? secret = null)
    {
        payload = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;

        // Trim user paste cruft (spaces, surrounding quotes) before splitting.
        var trimmed = key.Trim().Trim('"');
        var parts = trimmed.Split('-');
        if (parts.Length != 3) return false;
        if (parts[0] != Prefix) return false;
        if (parts[1].Length == 0 || parts[2].Length == 0) return false;

        byte[] payloadBytes;
        byte[] sigBytes;
        try
        {
            payloadBytes = Base32Decode(parts[1]);
            sigBytes = Base32Decode(parts[2]);
        }
        catch
        {
            return false;
        }
        if (sigBytes.Length != SigBytes) return false;

        var expected = Sign(payloadBytes, secret ?? DefaultSecret);
        if (!CryptographicOperations.FixedTimeEquals(sigBytes, expected)) return false;

        payload = Encoding.UTF8.GetString(payloadBytes);
        return true;
    }

    /// <summary>
    /// Issues a key for the given payload. Intended for the issuer side (the
    /// Gumroad webhook / generator script); the app itself never calls this in
    /// production. Exposed for tests and for the generator to share signing math.
    /// </summary>
    public static string Issue(string payload, string? secret = null)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var sig = Sign(payloadBytes, secret ?? DefaultSecret);
        return $"{Prefix}-{Base32Encode(payloadBytes)}-{Base32Encode(sig)}";
    }

    private static byte[] Sign(byte[] payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var full = HMACSHA256.HashData(keyBytes, payload);
        var truncated = new byte[SigBytes];
        Array.Copy(full, truncated, SigBytes);
        return truncated;
    }

    // ---- RFC 4648 base32, no padding, uppercase A-Z2-7 ----

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(byte[] data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0, bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
        {
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        var output = new List<byte>(input.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;
        foreach (var raw in input)
        {
            var c = char.ToUpperInvariant(raw);
            var idx = Base32Alphabet.IndexOf(c);
            if (idx < 0) throw new FormatException($"Invalid base32 character: {raw}");
            buffer = (buffer << 5) | idx;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return output.ToArray();
    }
}
