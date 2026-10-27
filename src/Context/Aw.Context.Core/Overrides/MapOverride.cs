using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Overrides;

/// <summary>One human decision about the map (spec §6.1, table map_override). It names its target by key, so it survives rebuilds, imports and key migrations (through aliases).</summary>
/// <param name="Action">What it does.</param>
/// <param name="Target">The node it applies to (for add, the new node's key).</param>
/// <param name="Who">The OS user at the terminal who made it.</param>
/// <param name="At">When it was made; an import keeps the file's value.</param>
/// <param name="Label">The new label (rename, add), or null.</param>
/// <param name="Other">The node to merge into or move under (merge, move; for add, its parent feature), or null.</param>
/// <param name="Rejection">For reject: the rejected proposal's <see cref="RejectionIdentity"/>; null rejects whatever the target is.</param>
/// <param name="Link">For accept or reject of an edge (a suggested file link): its id; the target is then the edge's 'from' node. Null for a node decision. Omitted from JSON when null.</param>
/// <param name="Undoes">For undo: the override it reverses. Omitted from JSON when null.</param>
/// <param name="Proposal">For propose: the proposal's run, repo, kind, parent and evidence. Omitted from JSON when null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MapOverride(
    OverrideAction Action,
    NodeKey Target,
    string Who,
    DateTimeOffset At,
    string? Label = null,
    NodeKey? Other = null,
    string? Rejection = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LinkId? Link = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OverrideId? Undoes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProposalPayload? Proposal = null)
{
    /// <summary>
    /// For a reject read through a deny cut: the identity of the same proposal hashed over the evidence the globs leave, when the row was written before the glob and hashes the full evidence.
    /// The row's <see cref="Rejection"/> stays as stored, so its id (and an undo of it) do not change; never stored, never part of the id.
    /// </summary>
    [JsonIgnore]
    public string? AllowedRejection { get; init; }

    /// <summary>The 'who' of an undo row a deny cut writes for a decision about a node or link it takes out (see <see cref="UndoingCut"/>).</summary>
    public const string CutMarker = "service:deny-cut";

    /// <summary>The 'who' of an undo row a build writes for an accept of an 'implements' edge from a file its repo demotes (see <see cref="UndoingDemote"/>).</summary>
    public const string DemoteMarker = "service:demote-cut";

    /// <summary>The 'who' of every decision the engine itself writes (an accept of a draft's deterministic items, a scope proposal).</summary>
    public const string EngineWho = "service:engine";

    /// <summary>The 'who' of an undo row a build writes for an engine accept of an edge the new map rules no longer find (see <see cref="UndoingRedecide"/>).</summary>
    public const string RedecideMarker = "service:rules-redecide";

    /// <summary>Whether this is an undo row a deny cut wrote (never a person's).</summary>
    [JsonIgnore]
    public bool IsCutUndo => Action == OverrideAction.Undo && Who == CutMarker;

    /// <summary>The undo row that takes a person's decision out with the node or link a deny cut removes.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="at">When.</param>
    /// <returns>The undo row, marked as the cut's.</returns>
    public static MapOverride UndoingCut(MapOverride decision, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new MapOverride(OverrideAction.Undo, decision.Target, CutMarker, at, Undoes: decision.ToId());
    }

    /// <summary>The undo row that takes an accept out with the 'implements' edge a demote glob drops from the map.</summary>
    /// <param name="decision">The accept.</param>
    /// <param name="at">When.</param>
    /// <returns>The undo row, marked as the demote cut's.</returns>
    public static MapOverride UndoingDemote(MapOverride decision, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new MapOverride(OverrideAction.Undo, decision.Target, DemoteMarker, at, Undoes: decision.ToId());
    }

    /// <summary>The undo row that takes an engine accept out with the edge a rules change no longer finds.</summary>
    /// <param name="decision">The accept.</param>
    /// <param name="at">When.</param>
    /// <returns>The undo row, marked as the rules re-decision's.</returns>
    public static MapOverride UndoingRedecide(MapOverride decision, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new MapOverride(OverrideAction.Undo, decision.Target, RedecideMarker, at, Undoes: decision.ToId());
    }

    /// <summary>Whether this reject names a proposal of the given <see cref="RejectionIdentity"/>: any proposal when it has no identity, otherwise by the identity it was written with or the one the deny cut gave it.</summary>
    /// <param name="identity">The proposal's identity.</param>
    /// <returns>True when it does.</returns>
    public bool Rejects(string identity) => Rejection is null || Rejection == identity || AllowedRejection == identity;

    /// <summary>The id: a hash of the content, so the same decision imported twice is one row (a method: it is never read from a file).</summary>
    /// <returns>The id.</returns>
    public OverrideId ToId()
    {
        // A proposal is the same row whenever it was planned, so two concurrent proposes of one surface write it once.
        var when = Action == OverrideAction.Propose ? string.Empty : At.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);
        var text = $"{Action}\n{Target.Value}\n{Who}\n{when}\n{Label}\n{Other?.Value}\n{Rejection}";
        text += ReviewText();
        return OverrideId.From("ovr_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16]);
    }

    // Only the S-4 fields join the hash, and only when set, so every override written before them keeps its id.
    private string ReviewText() =>
        Link is null && Undoes is null && Proposal is null
            ? string.Empty
            : $"\n{Link?.Value}\n{Undoes?.Value}\n{Proposal?.ToHashText()}";
}
