using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>Reads the owner's login files for the owner-copy source: when each login runs out, and what of the Claude Code settings file may be copied into a session.</summary>
internal static class LoginFiles
{
    internal static DateTimeOffset? ClaudeExpiry(JsonElement root) =>
        root.TryGetProperty("claudeAiOauth", out var oauth) && oauth.TryGetProperty("expiresAt", out var at) && at.TryGetInt64(out var ms) ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

    // The earlier of the access token's own 'exp' and Codex's refresh age after 'last_refresh' (spike item 1: Codex refreshes an older login when it starts,
    // whatever the token still has left, and a refresh in the copy rotates the owner's refresh token, spike item 2).
    internal static DateTimeOffset? CodexExpiry(JsonElement root)
    {
        var exp = root.TryGetProperty("tokens", out var tokens) && tokens.TryGetProperty("access_token", out var jwt) && jwt.ValueKind == JsonValueKind.String
            ? JwtExpiry(jwt.GetString())
            : null;
        var stale = RefreshAgeExpiry(root);
        return (exp, stale) switch
        {
            ({ } e, { } r) => e < r ? e : r,
            ({ } e, null) => e,
            (null, var r) => r,
        };
    }

    private static DateTimeOffset? RefreshAgeExpiry(JsonElement root) =>
        root.TryGetProperty("last_refresh", out var last) && last.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(last.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var refreshed)
            ? refreshed + OwnerCopyOptions.CodexRefreshAge
            : null;

    private static DateTimeOffset? JwtExpiry(string? jwt)
    {
        var parts = jwt?.Split('.');
        if (parts is not { Length: 3 })
        {
            return null;
        }

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
        }
        catch (Exception e) when (e is JsonException or FormatException)
        {
            return null;
        }
    }

    // The CLI's own file is a login, not data to explain: a name is kept, a value that could be a credential is emptied.
    internal static byte[]? WithoutMcpSecrets(byte[] json)
    {
        try
        {
            var root = JsonNode.Parse(json);
            EmptyMcpValues(root);
            return Encoding.UTF8.GetBytes(root?.ToJsonString() ?? "{}");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void EmptyMcpValues(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, child) in obj.ToList())
                {
                    if (string.Equals(name, "mcpServers", StringComparison.Ordinal) && child is JsonObject servers)
                    {
                        foreach (var server in servers.Select(s => s.Value).OfType<JsonObject>())
                        {
                            EmptyValues(server["env"]);
                            EmptyValues(server["headers"]);
                        }
                    }
                    else
                    {
                        EmptyMcpValues(child);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    EmptyMcpValues(item);
                }

                break;
            default:
                break;
        }
    }

    private static void EmptyValues(JsonNode? map)
    {
        if (map is JsonObject values)
        {
            foreach (var key in values.Select(v => v.Key).ToList())
            {
                values[key] = string.Empty;
            }
        }
    }
}
