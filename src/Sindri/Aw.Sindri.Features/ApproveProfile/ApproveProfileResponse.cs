using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ApproveProfile;

/// <summary>The live hash, the key paths that differ from the latest approval, and whether it is now approved.</summary>
/// <param name="Hash">The live profile's hash.</param>
/// <param name="Changes">Key paths changed since the latest approval (every path when there was none).</param>
/// <param name="Approved">True when this hash is now (or already was) the approval.</param>
/// <param name="Sequence">The 'profile.approved' event's ledger sequence when this call approved it; null for a preview or an approval that already held.</param>
public sealed record ApproveProfileResponse(ProfileHash Hash, IReadOnlyList<string> Changes, bool Approved, long? Sequence)
{
    /// <summary>For each <c lang="text">agents.&lt;id&gt;.stepArgvHash</c> the approval adds or changes, the entry on this box that has the hash (launcher L-16); empty when none changed.</summary>
    public IReadOnlyList<StepArgvLine> StepArgv { get; init; } = [];

    /// <summary>Why the stored approval couldn't be read, when it couldn't (the changes then list every key); null, and left out of the JSON, otherwise (#1143).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Unreadable { get; init; }

    /// <summary>True when the hash differs from the latest approval's but no key path does: the preview never says "nothing" then (#1147); left out of the JSON otherwise.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool UnlistedChange { get; init; }
}
