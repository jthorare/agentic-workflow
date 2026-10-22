using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Checks;

/// <summary>What a phase packet says the build may touch (line spec §3.2 row 4): the commit it builds on, the files it may write and the existing tests it may change.</summary>
/// <param name="BaseSha">The commit the branch builds on ('baseSha' of packet.json).</param>
/// <param name="AllowedFiles">The write scope ('allowedFiles' of phase.json): globs and 'new: path'.</param>
/// <param name="TestChange">The existing test files the phase may change ('testChange' of phase.json).</param>
public sealed record PhaseScope(string BaseSha, IReadOnlyList<string> AllowedFiles, IReadOnlyList<string> TestChange)
{
    /// <summary>Reads the scope from a packet's files.</summary>
    /// <param name="files">File name to text: packet.json and phase.json.</param>
    /// <returns>The scope; an error naming what the packet lacks.</returns>
    public static Result<PhaseScope> From(IReadOnlyDictionary<string, string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var packet = Object(files, "packet.json");
        var phase = Object(files, "phase.json");
        if (packet?["baseSha"] is not JsonValue sha || !sha.TryGetValue<string>(out var baseSha) || string.IsNullOrWhiteSpace(baseSha) || phase?["allowedFiles"] is not JsonArray allowed)
        {
            return SindriErrors.InvalidInput.With("the phase packet must hold packet.json with a baseSha and phase.json with allowedFiles");
        }

        var testChange = phase["testChange"] is JsonArray listed ? listed.Select(Path).OfType<string>().ToList() : [];
        return new PhaseScope(baseSha, [.. allowed.Select(static a => a is JsonValue v && v.TryGetValue<string>(out var text) ? text : null).OfType<string>()], testChange);
    }

    // An entry names its path as { path, reason } or as the bare path.
    private static string? Path(JsonNode? entry) => entry switch
    {
        JsonValue v when v.TryGetValue<string>(out var text) => text,
        JsonObject o when o["path"] is JsonValue p && p.TryGetValue<string>(out var text) => text,
        _ => null,
    };

    private static JsonObject? Object(IReadOnlyDictionary<string, string> files, string name)
    {
        try
        {
            return files.TryGetValue(name, out var text) ? JsonNode.Parse(text) as JsonObject : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: a file that is not JSON is the same answer as a missing one
        }
    }
}
