using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraphService.Models;

public sealed record CursorPayload(
    [property: JsonPropertyName("ts")] DateTime TimestampUtc,
    [property: JsonPropertyName("id")] string Id);

public static class CursorCodec
{
    public static string Encode(CursorPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(string? cursor, out CursorPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return false;
        }

        try
        {
            var raw = Convert.FromBase64String(cursor);
            var json = Encoding.UTF8.GetString(raw);
            var decoded = JsonSerializer.Deserialize<CursorPayload>(json);
            if (decoded is null || string.IsNullOrWhiteSpace(decoded.Id))
            {
                return false;
            }

            payload = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
