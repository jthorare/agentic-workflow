using System.Text.Json.Serialization;
using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core.Concepts;

/// <summary>One pin as a query sees it: what is inside it (concept pins spec §3.1). The map's own grouping decides, so a file is in one pin.</summary>
public sealed class PinScope
{
    private const string RecordPrefix = "record:";

    private readonly PinSet _set;

    internal PinScope(PinSet set, ConceptId pin)
    {
        (_set, Pin) = (set, pin);
    }

    /// <summary>The pin.</summary>
    [JsonRequired]   // why: never serialized; ARCH-ID-002 wants every typed id required on read, and the constructor stays internal so only PinScope.Of makes one
    public ConceptId Pin { get; }

    /// <summary>The pin <paramref name="pin"/> names, as the caller may see it.</summary>
    /// <param name="mapped">The repos the map is built from; the ids are made over these, so 'billing' is the pin the map shows.</param>
    /// <param name="visible">The caller's repos; a pin only they don't see answers like a typo.</param>
    /// <param name="pin">A pin id or 'concept:&lt;id&gt;'.</param>
    /// <param name="graph">The map's whole stored graph (before it is cut to the caller's scope).</param>
    /// <param name="mapTag">How answers name the map ('v41').</param>
    /// <returns>The scope, or SND-CONTEXT-102 (no such pin; at most 5 visible ones named), 103 (no pins) or 104 (the map has no concept node for it yet).</returns>
    public static Result<PinScope> Of(IReadOnlyList<ContextRepo> mapped, IReadOnlyList<ContextRepo> visible, string pin, GraphSnapshot graph, string mapTag) =>
        PinSet.Of(mapped, visible, graph).Scope(pin, mapTag);

    /// <summary>Whether a repo file is in this pin: the first pin of that repo whose paths match it holds it.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="repoRelativePath">The path in the repo.</param>
    /// <returns>True when this pin holds it.</returns>
    public bool HoldsPath(ContextRepoName repo, string repoRelativePath) => string.Equals(_set.PathPin(repo, repoRelativePath), Pin.Value, StringComparison.Ordinal);

    /// <summary>Whether a node is in this pin: its concept, a member feature, a code file through its home feature, or a record or decision that mentions either.</summary>
    /// <param name="key">The node.</param>
    /// <returns>True when this pin holds it.</returns>
    public bool HoldsNode(NodeKey key) => _set.PinOf(key) is { } held && string.Equals(held.Value, Pin.Value, StringComparison.Ordinal);

    /// <summary>Whether a record is in this pin: the record node <c xml:lang="en">record:&lt;key&gt;</c> is held through a link to the pin or a member feature. One that is not is counted in <paramref name="skipped"/> under the pin it sits in.</summary>
    /// <param name="record">The record.</param>
    /// <param name="skipped">Where a left-out record is counted, or null.</param>
    /// <returns>True when this pin holds it.</returns>
    public bool HoldsRecord(RecordKey record, PinTally? skipped)
    {
        var node = NodeKey.TryFrom(RecordPrefix + record.Value, out var key) ? key : (NodeKey?)null;
        var held = node is { } found && HoldsNode(found);
        if (!held)
        {
            skipped?.Count(node is { } left ? _set.VisiblePinOf(left) : null);
        }

        return held;
    }

    /// <summary>Whether a repo doc is in this pin: its record is linked in, or failing a link its path is inside the pin's code paths. One that is not is counted in <paramref name="skipped"/>.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="repoRelativePath">The path in the repo.</param>
    /// <param name="skipped">Where a left-out doc is counted, or null.</param>
    /// <returns>True when this pin holds it.</returns>
    public bool HoldsDoc(ContextRepoName repo, string repoRelativePath, PinTally? skipped)
    {
        var linked = RepoDocs.TryKey(repo, repoRelativePath, out var record) && NodeKey.TryFrom(RecordPrefix + record.Value, out var node) ? node : (NodeKey?)null;
        var held = (linked is { } key && HoldsNode(key)) || HoldsPath(repo, repoRelativePath);
        if (!held)
        {
            skipped?.Count((linked is { } left ? _set.VisiblePinOf(left) : null) ?? _set.VisiblePathPin(repo, repoRelativePath));
        }

        return held;
    }

    /// <summary>The records of the map this pin holds (the ones the dense leg's scan is cut to).</summary>
    /// <returns>Their keys.</returns>
    public IReadOnlySet<RecordKey> Records() =>
        _set.KeysHeldBy(Pin, RecordPrefix).Select(k => RecordKey.TryFrom(k[RecordPrefix.Length..], out var record) ? record : (RecordKey?)null).OfType<RecordKey>().ToHashSet();

    /// <summary>Whether the map holds a file in this pin at all.</summary>
    /// <returns>False for a pin whose globs and features match nothing.</returns>
    public bool HoldsAnyFile() => _set.KeysHeldBy(Pin, "code:").Any();

    /// <summary>The pin an anchor sits in.</summary>
    /// <param name="anchor">The anchor.</param>
    /// <param name="mapped">The repos the map is built from.</param>
    /// <param name="graph">The map's graph.</param>
    /// <returns>The pin, or null when the anchor is in none.</returns>
    public static ConceptId? PinOf(NodeKey anchor, IReadOnlyList<ContextRepo> mapped, GraphSnapshot graph) => PinSet.Of(mapped, mapped, graph).PinOf(anchor);
}
