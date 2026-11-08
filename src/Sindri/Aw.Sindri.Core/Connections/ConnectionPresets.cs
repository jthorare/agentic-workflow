using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Connections;

/// <summary>Reads a preset file.</summary>
public static class ConnectionPresets
{
    private static readonly string[] _requiredRest = ["id", "displayName", "hostGroup", "baseUrl", "keyEnv"];
    private static readonly string[] _requiredText = ["id", "displayName", "kind", "authorizeUrl", "tokenUrl", "hostGroup", "signInNote"];
    private static readonly string[] _httpsUrls = ["authorizeUrl", "tokenUrl", "revokeUrl", "probeUrl"];

    /// <summary>Parses one preset's JSON (camelCase keys: id (read as Key), displayName, kind, authorizeUrl, tokenUrl, revokeUrl, defaultScopes, hostGroup, loopbackAllowed, signInNote, pollableEvents, probeUrl).</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>The preset, or invalid input naming the key that is wrong.</returns>
    public static Result<ConnectionPreset> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            return document.RootElement.ValueKind == JsonValueKind.Object ? Build(document.RootElement) : Bad("the file is not a JSON object");
        }
        catch (JsonException)
        {
            return Bad("the file is not JSON");
        }
    }

    private static Result<ConnectionPreset> Build(JsonElement root) =>
        string.Equals(Text(root, "kind"), "rest", StringComparison.Ordinal) ? BuildRest(root) : BuildOAuth(root);

    // A rest preset (PR 9b, the 'jev' one): a key read from an environment variable, sent to one https URL. No sign-in, so none of the oauth fields.
    private static Result<ConnectionPreset> BuildRest(JsonElement root)
    {
        if (RestProblem(root) is { } problem)
        {
            return Bad(problem);
        }

        return new ConnectionPreset(
            Key: Text(root, "id") ?? string.Empty,
            DisplayName: Text(root, "displayName") ?? string.Empty,
            Kind: ConnectionKind.Rest,
            AuthorizeUrl: string.Empty,
            TokenUrl: string.Empty,
            RevokeUrl: null,
            DefaultScopes: [],
            HostGroup: Text(root, "hostGroup") ?? string.Empty,
            LoopbackAllowed: false,
            SignInNote: Text(root, "signInNote") ?? string.Empty,
            PollableEvents: [],
            ProbeUrl: null,
            BaseUrl: Text(root, "baseUrl"),
            KeyEnv: Text(root, "keyEnv"));
    }

    private static string? RestProblem(JsonElement root)
    {
        var missing = _requiredRest.FirstOrDefault(name => Text(root, name) is not { Length: > 0 });
        if (missing is not null)
        {
            return $"'{missing}' is missing";
        }

        if (Text(root, "baseUrl") is not { } url || !url.StartsWith("https://", StringComparison.Ordinal))
        {
            return "'baseUrl' is not an https URL";
        }

        return Text(root, "keyEnv") is { } env && env.All(static c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '_')
            ? null
            : "'keyEnv' is not an environment variable name (capitals, digits and '_')";
    }

    private static Result<ConnectionPreset> BuildOAuth(JsonElement root)
    {
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in _requiredText)
        {
            if (Text(root, name) is not { Length: > 0 } value)
            {
                return Bad($"'{name}' is missing");
            }

            text[name] = value;
        }

        var optional = new Dictionary<string, string?>(StringComparer.Ordinal) { ["revokeUrl"] = Text(root, "revokeUrl"), ["probeUrl"] = Text(root, "probeUrl") };
        if (PlainHttp(text, optional) is { } plain)
        {
            return Bad($"'{plain}' is not an https URL");
        }

        var kind = ConnectionWire.ParseKind(text["kind"]);
        var scopes = Strings(root, "defaultScopes");
        var events = Strings(root, "pollableEvents");
        if (kind.IsError || scopes is null || events is null || Flag(root, "loopbackAllowed") is not { } loopback)
        {
            return Bad("'kind', 'defaultScopes', 'pollableEvents' or 'loopbackAllowed' is missing or has the wrong type");
        }

        return new ConnectionPreset(text["id"], text["displayName"], kind.Value, text["authorizeUrl"], text["tokenUrl"], optional["revokeUrl"], scopes, text["hostGroup"], loopback, text["signInNote"], events, optional["probeUrl"]);
    }

    private static string? PlainHttp(Dictionary<string, string> text, Dictionary<string, string?> optional) =>
        _httpsUrls.FirstOrDefault(name => (text.GetValueOrDefault(name) ?? optional.GetValueOrDefault(name)) is { } value && !value.StartsWith("https://", StringComparison.Ordinal));

    private static Error Bad(string what) => SindriErrors.InvalidInput.With("connection preset: " + what);

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? Flag(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;

    private static List<string>? Strings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var list = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } text)
            {
                return null;
            }

            list.Add(text);
        }

        return list;
    }
}
