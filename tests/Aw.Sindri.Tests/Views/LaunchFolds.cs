using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Views;

/// <summary>The launch events a test folds through the views harness, each about <see cref="A.FirstItemId"/> unless told otherwise.</summary>
internal static class LaunchFolds
{
    public static readonly AgentId ClaudeCode = AgentId.From("claude-code");

    /// <summary>Folds the item's creation, in <paramref name="repo"/>.</summary>
    public static Task Item(this ViewsHarness v, RepoName? repo, ItemId? item = null) =>
        v.Fold(EventTypes.ItemCreated, new ItemCreated(v.Slice.Ids.New<CommandId>(), item ?? A.FirstItemId, "Ship it", repo, Link: null, Body: null, Owner: null), item ?? A.FirstItemId);

    /// <summary>Folds an 'item.open-requested'.</summary>
    public static Task Request(this ViewsHarness v, LaunchId? launch = null, AgentId? target = null, LaunchMode mode = LaunchMode.Open, string chosenBy = "click", ItemId? item = null) =>
        v.Fold(EventTypes.ItemOpenRequested, new ItemOpenRequested(launch ?? A.FirstLaunchId, item ?? A.FirstItemId, mode, target ?? ClaudeCode, chosenBy), item ?? A.FirstItemId);

    /// <summary>Folds a 'launch.started' whose viewer did <paramref name="outcome"/>.</summary>
    public static Task Start(this ViewsHarness v, LaunchId? launch = null, ViewerOutcomeKind outcome = ViewerOutcomeKind.Opened, SessionId? session = null, AgentId? target = null, LaunchMode mode = LaunchMode.Open, string? viewerDetail = null) =>
        v.Fold(EventTypes.LaunchStarted,
            new LaunchStarted(launch ?? A.FirstLaunchId, A.FirstItemId, mode, target ?? ClaudeCode, session, "sindri " + A.FirstItemId.Value,
                new LaunchViewer("warp", "Warp", outcome, outcome == ViewerOutcomeKind.Failed ? "SND-LAUNCH-001" : null, viewerDetail), ["tmux", "attach"], CatalogSource.Builtin, "sha256:abc"), A.FirstItemId);

    /// <summary>Folds a 'launch.failed'.</summary>
    public static Task Fail(this ViewsHarness v, string code = "SND-LAUNCH-007", LaunchStage? stage = null, LaunchId? launch = null, string message = "the launch was interrupted", string? reason = null, string? source = null) =>
        v.Fold(EventTypes.LaunchFailed, new LaunchFailed(launch ?? A.FirstLaunchId, A.FirstItemId, code, message, stage, reason, source), A.FirstItemId);

    /// <summary>Folds a 'launch.viewer-attached'.</summary>
    public static Task Attached(this ViewsHarness v, LaunchId? launch = null) =>
        v.Fold(EventTypes.LaunchViewerAttached, new LaunchViewerAttached(launch ?? A.FirstLaunchId, v.Slice.Clock.GetUtcNow()), A.FirstItemId);

    /// <summary>Folds a 'launch.session-reported'.</summary>
    public static Task Reported(this ViewsHarness v, SessionId session, LaunchId? launch = null) =>
        v.Fold(EventTypes.LaunchSessionReported, new LaunchSessionReported(launch ?? A.FirstLaunchId, session), A.FirstItemId);

    /// <summary>Folds a 'session.ended'.</summary>
    public static Task End(this ViewsHarness v, SessionEndReason reason = SessionEndReason.Exit, LaunchId? launch = null) =>
        v.Fold(EventTypes.SessionEnded, new SessionEnded(launch ?? A.FirstLaunchId, reason, reason == SessionEndReason.Exit ? 0 : null), A.FirstItemId);

    /// <summary>Folds a 'session.ended' for a limit kill.</summary>
    public static Task EndByLimit(this ViewsHarness v, LimitEnd limit, LaunchId? launch = null) =>
        v.Fold(EventTypes.SessionEnded, SessionEnded.Limited(launch ?? A.FirstLaunchId, limit), A.FirstItemId);
}
