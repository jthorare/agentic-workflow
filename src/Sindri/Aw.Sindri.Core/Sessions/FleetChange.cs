using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>One change worth a ledger row (collector spec CO3).</summary>
/// <param name="Ref">The session.</param>
/// <param name="Kind">What changed.</param>
/// <param name="State">The state when it was seen.</param>
/// <param name="ObservedAt">When the pass saw it.</param>
public sealed record FleetChange(SessionRef Ref, FleetChangeKind Kind, SessionState State, DateTimeOffset ObservedAt)
{
    /// <summary>Why it is asking, for an 'asking' change.</summary>
    public AskingReason? Reason { get; init; }

    /// <summary>The profile repo, when resolved.</summary>
    public RepoName? Repo { get; init; }

    /// <summary>The host's kind.</summary>
    public string HostKind { get; init; } = "unknown";

    /// <summary>The Sindri launch the session belongs to.</summary>
    public LaunchId? LaunchId { get; init; }
}
