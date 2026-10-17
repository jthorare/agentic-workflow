using System.Collections.Frozen;
using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): action labels keyed by catalog action id, and the controls' words (§7, §4.1, §12.4).</summary>
internal static partial class Labels
{
    // Labels by action id (§12.4: "labels live in one resource file keyed by action id"). An id not here shows a humanised form of itself.
    private static readonly FrozenDictionary<string, string> _actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ApplyCommand:answer-accept"] = "Accept",
        ["ApplyCommand:answer-grant"] = "Grant rounds",
        ["ApplyCommand:answer-grant-tokens"] = "Grant tokens",
        ["ApplyCommand:answer-reject"] = "Reject",
        ["ApplyCommand:answer-resume"] = "Resume",
        ["ApplyCommand:answer-text"] = "Answer",
        ["ApplyCommand:cancel"] = "Cancel instance",
        ["ApplyCommand:command"] = "Send workflow command",
        ["ApplyCommand:handback"] = "Hand back",
        ["ApplyCommand:takeover"] = "Take over",
        ["ApproveProfile"] = "Approve profile",
        ["ApproveRecipe"] = "Approve recipe",
        ["BuildContextMap"] = "Build map",
        ["RunContextBacktest"] = "Run map backtest",
        ["CancelJob"] = "Cancel job",
        ["CloseItem:cancelled"] = "Close as cancelled",
        ["CloseItem:done"] = "Close item",
        ["CompleteStep"] = "Complete",
        ["CreateItem"] = "Create item",
        ["Decide"] = "Decide",
        ["FileRequest"] = "File a request",
        ["ImportContextMap"] = "Import context map",
        ["IssueChallenge"] = "Get approval challenge",
        ["IssueNonce"] = "Get command nonce",
        ["OpenItem:attach"] = "Attach",
        ["SessionExit"] = "Report session exit",
        ["SessionHeartbeat"] = "Report session heartbeat",
        ["OpenItem:open"] = "Open",
        ["OpenItem:resume"] = "Resume session",
        ["PauseService"] = "Pause service",
        ["LaunchInit"] = "Set up the launcher",
        ["LaunchShow"] = "Show a launch",
        ["ProbeLaunchTargets"] = "Check launch targets again",
        ["PromoteContextMap"] = "Promote map",
        ["ProposeContextMap"] = "Propose map from scope",
        ["RebuildViews"] = "Rebuild views",
        ["RegisterRecipe"] = "Register recipe",
        ["ResumeService"] = "Resume service",
        ["ReviewContextMap:accept"] = "Accept map suggestion",
        ["ReviewContextMap:accept-deterministic"] = "Accept all deterministic links",
        ["ReviewContextMap:add"] = "Add map entry",
        ["ReviewContextMap:apply-file"] = "Apply map review file",
        ["ReviewContextMap:delete"] = "Delete map entry",
        ["ReviewContextMap:drop"] = "Drop map entry",
        ["ReviewContextMap:merge"] = "Merge map entries",
        ["ReviewContextMap:move"] = "Move map entry",
        ["ReviewContextMap:reject"] = "Reject map suggestion",
        ["ReviewContextMap:rename"] = "Rename map entry",
        ["ReviewContextMap:repoint"] = "Repoint map entry",
        ["ReviewContextMap:undo"] = "Undo map review",
        ["RevokeRecipe"] = "Revoke recipe",
        ["RunDoctor"] = "Run doctor",
        ["RunService"] = "Run now",
        ["StartInstance"] = "Start workflow",
        ["StopProcess"] = "Stop Sindri",
        ["SyncSources"] = "Sync context sources",
        ["ValidateProfile"] = "Validate profile",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The Health page's catalog section.</summary>
    internal const string ActionsCard = "ACTIONS";

    /// <summary>The copy button.</summary>
    internal const string Copy = "Copy";

    /// <summary>After a copy, for 1.5 s (§4.1, §12.4).</summary>
    internal const string Copied = "Copied";

    /// <summary>The copy block's line (§7, §12.4).</summary>
    internal const string TypedConfirmation = "Needs a typed confirmation at a terminal.";

    /// <summary>An action's label, keyed by its catalog id; an id the app has no label for shows a humanised form of it ("Some new thing"), never the raw "X:y".</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The label.</returns>
    internal static string Action(string action) => _actions.TryGetValue(action, out var label) ? label : Humanise(action);

    /// <summary>True when the app has its own label for an action id (a catalog row without one shows a humanised id).</summary>
    /// <param name="action">The action id.</param>
    /// <returns>True when labelled.</returns>
    internal static bool HasLabel(string action) => _actions.ContainsKey(action);

    // "ApplyCommand:pause-all" -> "Apply command pause all": camel-case words split and lower-cased after the first, ':' and '-' as spaces.
    private static string Humanise(string action)
    {
        var words = new System.Text.StringBuilder();
        var previous = ' ';
        foreach (var c in action)
        {
            var separator = c is ':' or '-' or '_';
            if (char.IsUpper(c) && char.IsLower(previous))
            {
                _ = words.Append(' ');
            }

            _ = words.Append(separator ? ' ' : words.Length == 0 ? char.ToUpperInvariant(c) : char.IsUpper(c) ? char.ToLowerInvariant(c) : c);
            previous = c;
        }

        return words.ToString().Trim();
    }

    /// <summary>What Decide's line says while the command catalog has not been read.</summary>
    internal const string CheckingCatalog = "Checking what can run";

    /// <summary>What Decide's line says when the catalog is read but lists no Decide and the decision serves no command.</summary>
    internal const string DecideUnlisted = "Decide is not in the command catalog";

    /// <summary>The name of the Copy button under a decision's Decide line.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <returns>The words.</returns>
    internal static string CopyDecide(string decisionId) => Copy + ": Decide " + decisionId;

    /// <summary>The accessible name of a decision's Decide line: the decision, then the line.</summary>
    /// <param name="decisionId">The decision.</param>
    /// <param name="line">The line.</param>
    /// <returns>The words.</returns>
    internal static string DecideLineName(string decisionId, string line) => decisionId + ": " + line;

    /// <summary>A pending control's phase: "arrives in P2".</summary>
    /// <param name="phase">The phase the catalog names.</param>
    /// <returns>The words.</returns>
    internal static string Arrives(string phase) => "arrives in " + phase;

    /// <summary>The accepted outcome (§4.1, §12.4: built from the action's label): "Rebuild views: done".</summary>
    /// <param name="label">The action's label.</param>
    /// <returns>The words.</returns>
    internal static string Done(string label) => label + ": done";

    /// <summary>A destructive control's in-app question (§8): its name, asked ("Close as cancelled ITM-…?").</summary>
    /// <param name="named">The control's label and subject.</param>
    /// <returns>The question.</returns>
    internal static string ConfirmQuestion(string named) => named + "?";

    /// <summary>Health's line over the pending rows (§4.8): "12 actions arrive in later phases".</summary>
    /// <param name="count">How many rows are pending.</param>
    /// <returns>The words.</returns>
    internal static string PendingSummary(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "action arrives" : "actions arrive")} in later phases");

    /// <summary>The health pill's word for the status's health.word (E-14); a word this build doesn't know shows as served.</summary>
    /// <param name="word">'healthy', 'degraded', 'stalled' or 'rebuilding'.</param>
    /// <returns>The pill's word.</returns>
    internal static string HealthWord(string word) => word switch
    {
        "healthy" => "Healthy",
        "degraded" => "Degraded",
        "stalled" => "Stalled",
        "rebuilding" => "Rebuilding",
        _ => word,
    };
}
