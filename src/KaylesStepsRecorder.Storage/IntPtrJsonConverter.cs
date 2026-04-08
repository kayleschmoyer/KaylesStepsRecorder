using System.Text.Json;
using System.Text.Json.Serialization;

namespace KaylesStepsRecorder.Storage;

/// <summary>
/// Custom JSON converter for <see cref="IntPtr"/> values.
/// Serializes as a 64-bit integer and deserializes back to <see cref="IntPtr"/>.
/// Handles zero and negative values gracefully.
/// </summary>
public sealed class IntPtrJsonConverter : JsonConverter<IntPtr>
{
    public override IntPtr Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return IntPtr.Zero;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            long value = reader.GetInt64();
            return new IntPtr(value);
        }

        // Handle the case where an IntPtr was serialized as a string (e.g., "0").
        if (reader.TokenType == JsonTokenType.String)
        {
            string? text = reader.GetString();
            if (string.IsNullOrEmpty(text) || !long.TryParse(text, out long parsed))
            {
                return IntPtr.Zero;
            }

            return new IntPtr(parsed);
        }

        throw new JsonException($"Unexpected token type '{reader.TokenType}' when deserializing IntPtr.");
    }

    public override void Write(Utf8JsonWriter writer, IntPtr value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.ToInt64());
    }
}
