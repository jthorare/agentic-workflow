using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// A row of the 'launches' view (UI spec E-1, launcher L-10; keyed by launch id): one launch folded from 'item.open-requested' and the
/// launcher's outcome events. The row keeps the reported session id, so it survives 'sindri views rebuild'.
/// </summary>
/// <param name="Id">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Repo">The item's repo, or null.</param>
/// <param name="Mode">Open, attach or resume.</param>
/// <param name="Target">The agent.</param>
/// <param name="ChosenBy">'click', 'default', or 'engine' for an unattended launch nobody asked for.</param>
/// <param name="State">Requested, started or failed.</param>
/// <param name="RequestedAt">When it was asked for (an engine launch: when it started).</param>
/// <param name="OutcomeAt">When the outcome was recorded, or null while requested.</param>
/// <param name="Code">The failure's catalogue code, otherwise null.</param>
/// <param name="Message">The failure's message with its fix, otherwise null.</param>
/// <param name="Stage">Where a failure stopped, or null.</param>
/// <param name="SessionId">The session id known at start, otherwise null.</param>
/// <param name="ReportedSessionId">The id 'session wrap' reported, otherwise null.</param>
/// <param name="TabTitle">The viewer's title, once started.</param>
/// <param name="Viewer">What the viewer did, once started (its detail is private to this view).</param>
/// <param name="Late">True when a client attached after 'launch.started' recorded 'unconfirmed'.</param>
/// <param name="Attach">The command that attaches to the session, once started.</param>
/// <param name="CatalogSource">Where the entry came from, once started.</param>
/// <param name="ArgvHash">The hash of the entry's argv template, once started.</param>
/// <param name="Ended">How the session ended, or null while it runs.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record LaunchRow(
    LaunchId Id,
    ItemId Item,
    RepoName? Repo,
    LaunchMode Mode,
    AgentId Target,
    string ChosenBy,
    LaunchState State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? OutcomeAt,
    string? Code,
    string? Message,
    LaunchStage? Stage,
    SessionId? SessionId,
    SessionId? ReportedSessionId,
    string? TabTitle,
    LaunchViewer? Viewer,
    bool Late,
    IReadOnlyList<string>? Attach,
    CatalogSource? CatalogSource,
    string? ArgvHash,
    LaunchEnd? Ended,
    long Sequence)
{
    /// <summary>The session id a resume uses: the one known at start, else the reported one.</summary>
    public SessionId? ResumableId => SessionId ?? ReportedSessionId;

    /// <summary>True while the session exists: started and not ended.</summary>
    public bool Live => State == LaunchState.Started && Ended is null;

    /// <summary>The failure's sandbox reason (SND-LAUNCH-014 to -021), otherwise null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>The failure's credential source (SND-LAUNCH-017), otherwise null.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Source { get; init; }
}
