using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;

namespace Aw.Sindri.Core.Prove;

/// <summary>
/// The hidden-scenario store's file formats (line spec §9.1): <c language="text">set.json</c> is <c language="text">{schemaVersion, phase, scenarios[{id, item, kind, steps[], expected, phrase?}]}</c>
/// (the canary scenario has <c language="text">kind: "canary"</c>, <c language="text">item: null</c> and a <c language="text">phrase</c>), <c language="text">seal.json</c> is <c language="text">{schemaVersion, phase, setHash, planHash}</c>
/// and <c language="text">spent.json</c> is <c language="text">{schemaVersion, spent[id]}</c>. Both adapters read through this class, so they agree on what a file means.
/// </summary>
public static class HoldoutFiles
{
    /// <summary>The name of the set file.</summary>
    public const string SetName = "set.json";

    /// <summary>The name of the seal file.</summary>
    public const string SealName = "seal.json";

    /// <summary>The name of the spent marks.</summary>
    public const string SpentName = "spent.json";

    /// <summary>Whether text is one safe path segment: letters, digits, '.', '_' and '-', not starting with '.'.</summary>
    /// <param name="key">A line slug or phase key.</param>
    /// <returns>True when it can name a directory of the store.</returns>
    public static bool IsKey(string? key) => !string.IsNullOrEmpty(key) && key[0] != '.' && key.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');

    /// <summary>The hash a seal names for a set file.</summary>
    /// <param name="setText">The set file's text.</param>
    /// <returns>'sha256:' and the hex digest of the UTF-8 bytes.</returns>
    public static string Hash(string setText)
    {
        ArgumentNullException.ThrowIfNull(setText);
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(setText)));
    }

    /// <summary>Reads a set file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The set; an unreadable-store error when the text is not a set.</returns>
    public static Result<HoldoutSet> ParseSet(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var root = JsonNode.Parse(text);
            var scenarios = (root?["scenarios"] as JsonArray ?? []).OfType<JsonObject>().Select(Scenario).ToList();
            return (string?)root?["phase"] is { Length: > 0 } phase && scenarios.Count > 0 && scenarios.TrueForAll(static s => s.Key.Length > 0)
                ? new HoldoutSet(phase, scenarios, Hash(text))
                : Unreadable(SetName, "it names no phase or no scenario with an id");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(SetName, e.Message);
        }
    }

    /// <summary>Reads a seal file.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>The seal; an unreadable-store error when the text is not a seal.</returns>
    public static Result<HoldoutSeal> ParseSeal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        try
        {
            var root = JsonNode.Parse(text);
            return (string?)root?["setHash"] is { Length: > 0 } set && (string?)root["planHash"] is { Length: > 0 } plan
                ? new HoldoutSeal(set, plan)
                : Unreadable(SealName, "it names no setHash or planHash");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(SealName, e.Message);
        }
    }

    /// <summary>Reads the spent marks.</summary>
    /// <param name="text">The file's text; empty reads as no marks.</param>
    /// <returns>The marked ids; an unreadable-store error when the text is not a spent file.</returns>
    public static Result<IReadOnlySet<string>> ParseSpent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
        {
            return Results.Success<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
        }

        try
        {
            var ids = (JsonNode.Parse(text)?["spent"] as JsonArray)?.Select(static n => (string?)n).ToList();
            return ids is not null && ids.TrueForAll(static i => !string.IsNullOrEmpty(i))
                ? Results.Success<IReadOnlySet<string>>(ids.Select(static i => i!).ToHashSet(StringComparer.Ordinal)) // why: the check above
                : Unreadable(SpentName, "it has no 'spent' list of ids");
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(SpentName, e.Message);
        }
    }

    /// <summary>The text of a spent file.</summary>
    /// <param name="marked">The marked scenarios' keys.</param>
    /// <returns>The JSON text, ids sorted.</returns>
    public static string SpentJson(IEnumerable<string> marked)
    {
        ArgumentNullException.ThrowIfNull(marked);
        return new JsonObject { ["schemaVersion"] = 1, ["spent"] = new JsonArray([.. marked.Order(StringComparer.Ordinal).Select(static i => (JsonNode?)JsonValue.Create(i))]) }.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>The error for a file of the store that can't be read.</summary>
    /// <param name="file">The file's name.</param>
    /// <param name="why">What is wrong.</param>
    /// <returns>SND-BLOCK-021.</returns>
    public static Error Unreadable(string file, string why) => WorkflowErrors.HoldoutStoreUnavailable.With($"the hidden-scenario store's {file} can't be used: {why} (fix: the owner replaces the phase's hidden set)");

    private static HoldoutScenario Scenario(JsonObject s) => new(
        (string?)s["id"] ?? string.Empty,
        (string?)s["item"],
        (string?)s["kind"] ?? string.Empty,
        [.. (s["steps"] as JsonArray ?? []).Select(static t => (string?)t ?? string.Empty)],
        (string?)s["expected"] ?? string.Empty,
        (string?)s["phrase"]);
}
