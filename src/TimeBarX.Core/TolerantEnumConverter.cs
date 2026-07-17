using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeBarX.Core;

/// <summary>
/// A <see cref="JsonStringEnumConverter"/> replacement that never throws on an
/// unrecognized enum value. Unknown names (a value from a newer app version, a
/// hand-edited typo) and out-of-range numbers deserialize to <c>default</c> for
/// that enum type instead of aborting the whole document.
/// </summary>
/// <remarks>
/// This matters for settings.json: the stock string-enum converter throws a
/// <see cref="JsonException"/> on an unknown name, which would collapse the
/// entire load to <see cref="AppSettings.Default"/> and let the next Save wipe
/// every unrelated user setting. Falling back per-field to the enum default lets
/// <see cref="AppSettings.Sanitize"/> coerce it to a sane value while every
/// other field survives. Writing still emits the string name.
/// </remarks>
public sealed class TolerantEnumConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var wrapperType = typeof(TolerantConverter<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(wrapperType)!;
    }

    private sealed class TolerantConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        // A value that is (almost) never a defined member, so an unrecognized
        // payload survives Sanitize()'s Enum.IsDefined check and gets coerced to
        // the field's real default — rather than silently becoming the enum's
        // first member (default(T)), which is a valid-but-wrong value. Undefined
        // for every enum in this app (none use unchecked(-1)).
        private static readonly T Undefined = (T)Enum.ToObject(typeof(T), -1);

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    var name = reader.GetString();
                    // Case-insensitive to match JsonStringEnumConverter's default
                    // read behavior; unknown names fall back to an undefined value.
                    return Enum.TryParse<T>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                        ? parsed
                        : Undefined;

                case JsonTokenType.Number:
                    // Accept numeric payloads (older files, or values written by a
                    // converter without the string option). Out-of-range numbers
                    // pass through as undefined; Sanitize coerces those to defaults.
                    if (reader.TryGetInt64(out var raw) &&
                        Enum.TryParse<T>(raw.ToString(System.Globalization.CultureInfo.InvariantCulture), out var byNumber))
                    {
                        return byNumber;
                    }
                    return Undefined;

                default:
                    // Unexpected token shape: leave it to Sanitize.
                    reader.Skip();
                    return Undefined;
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }
}
