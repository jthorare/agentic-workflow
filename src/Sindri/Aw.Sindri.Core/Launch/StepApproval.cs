using System.Buffers;
using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Launch;

/// <summary>SL8: an unattended Step runs only approved argv. Pure.</summary>
public static class StepApproval
{
    /// <summary>Checks <paramref name="entry"/> for an unattended Step.</summary>
    /// <param name="entry">The loaded entry; the caller builds the argv from the entry this returns, never from a re-read.</param>
    /// <param name="builtinArgvHash">The built-in entry's hash for the same id, or null when there is no built-in of that id.</param>
    /// <param name="approvedHashes">The approved profile's agents.&lt;id&gt;.stepArgvHash list for this id (empty when none).</param>
    /// <returns>The same entry; SND-LAUNCH-012 with 'reason', 'current' and 'approved' when the argv isn't approved.</returns>
    public static Result<AgentEntry> Check(AgentEntry entry, string? builtinArgvHash, IReadOnlyList<string> approvedHashes)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Source == CatalogSource.Builtin || IsBinOnlyOverride(entry, builtinArgvHash))
        {
            return entry;
        }

        var listed = approvedHashes;
        if (entry.ArgvHash is { } current && listed.Contains(current, StringComparer.Ordinal))
        {
            return entry;
        }

        var reason = listed.Count == 0 ? "not-approved" : "hash-changed";
        return SindriErrors.LaunchNotApproved
            .With($"{entry.Id.Value} isn't approved for unattended Steps ({reason}; fix: run `sindri launch show {entry.Id.Value}` and put its argvHash under agents.{entry.Id.Value}.stepArgvHash, then approve the profile)")
            .WithExtension("reason", Text(reason))
            .WithExtension("current", Text(entry.ArgvHash ?? string.Empty))
            .WithExtension("approved", Texts(listed));
    }

    /// <summary>Resolves <paramref name="id"/> once and checks the entry it got.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="id">The agent id.</param>
    /// <param name="builtinArgvHash">The built-in entry's hash for <paramref name="id"/>, or null.</param>
    /// <param name="approvedHashes">The approved profile's agents.&lt;id&gt;.stepArgvHash list for this id (empty when none).</param>
    /// <returns>The entry; SND-LAUNCH-003 'not-in-catalog'; or what <see cref="Check"/> returns.</returns>
    public static Result<AgentEntry> Resolve(IAgentCatalog catalog, AgentId id, string? builtinArgvHash, IReadOnlyList<string> approvedHashes)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return catalog.Resolve(id) is { } entry
            ? Check(entry, builtinArgvHash, approvedHashes)
            : SindriErrors.LaunchAgentMissing.With($"{id.Value} is not in the catalog (not-in-catalog; fix: run `sindri launch show`)");
    }

    private static bool IsBinOnlyOverride(AgentEntry entry, string? builtinArgvHash) =>
        entry.Source == CatalogSource.Override && builtinArgvHash is not null && string.Equals(entry.ArgvHash, builtinArgvHash, StringComparison.Ordinal);

    private static JsonElement Text(string value) => Write(w => w.WriteStringValue(value));

    private static JsonElement Texts(IReadOnlyList<string> values) => Write(w =>
    {
        w.WriteStartArray();
        foreach (var value in values)
        {
            w.WriteStringValue(value);
        }

        w.WriteEndArray();
    });

    private static JsonElement Write(Action<Utf8JsonWriter> body)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            body(writer);
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
