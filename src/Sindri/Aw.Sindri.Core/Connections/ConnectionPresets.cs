using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>Reads a preset file.</summary>
public static class ConnectionPresets
{
    /// <summary>Parses one preset's JSON (camelCase keys: id (read as Key), displayName, kind, authorizeUrl, tokenUrl, revokeUrl, defaultScopes, hostGroup, loopbackAllowed, signInNote, pollableEvents, probeUrl).</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The preset, or invalid input naming the key that is wrong.</returns>
    public static Result<ConnectionPreset> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json ?? string.Empty);
        }
        catch (JsonException)
        {
            return Bad("the file is not JSON");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Bad("the file is not a JSON object");
            }

            var key = Text(root, "id");
            var display = Text(root, "displayName");
            var kind = Text(root, "kind");
            var authorize = Text(root, "authorizeUrl");
            var token = Text(root, "tokenUrl");
            var hostGroup = Text(root, "hostGroup");
            var note = Text(root, "signInNote");
            var scopes = Strings(root, "defaultScopes");
            var events = Strings(root, "pollableEvents");
            var revoke = Text(root, "revokeUrl");
            var probe = Text(root, "probeUrl");
            var loopback = root.TryGetProperty("loopbackAllowed", out var l) && l.ValueKind is JsonValueKind.True or JsonValueKind.False ? l.GetBoolean() : (bool?)null;

            foreach (var (name, value) in new[] { ("id", key), ("displayName", display), ("kind", kind), ("authorizeUrl", authorize), ("tokenUrl", token), ("hostGroup", hostGroup), ("signInNote", note) })
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return Bad($"'{name}' is missing");
                }
            }

            if (scopes is null)
            {
                return Bad("'defaultScopes' is missing");
            }

            if (events is null)
            {
                return Bad("'pollableEvents' is missing");
            }

            if (loopback is null)
            {
                return Bad("'loopbackAllowed' is missing");
            }

            foreach (var (name, value) in new[] { ("authorizeUrl", authorize), ("tokenUrl", token), ("revokeUrl", revoke), ("probeUrl", probe) })
            {
                if (value is not null && !value.StartsWith("https://", StringComparison.Ordinal))
                {
                    return Bad($"'{name}' is not an https URL");
                }
            }

            var parsedKind = ConnectionWire.ParseKind(kind!);
            if (parsedKind.IsError)
            {
                return Bad("'kind' is not a connection kind");
            }

            return new ConnectionPreset(key!, display!, parsedKind.Value, authorize!, token!, revoke, scopes, hostGroup!, loopback.Value, note!, events, probe);
        }
    }

    private static Error Bad(string what) => SindriErrors.InvalidInput.With("connection preset: " + what);

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string>? Strings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            list.Add(item.GetString()!);
        }

        return list;
    }
}
