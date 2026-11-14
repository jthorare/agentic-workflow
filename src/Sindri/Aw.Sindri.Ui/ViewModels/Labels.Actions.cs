using System.Collections.Frozen;

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
        ["ApplyCommand:cancel"] = "Cancel run",
        ["ApplyCommand:command"] = "Send workflow command",
        ["ApplyCommand:handback"] = "Hand back",
        ["ApplyCommand:rewind"] = "Go back a step",
        ["ApplyCommand:takeover"] = "Take over",
        ["AddRepo"] = "Add repo",
        ["AddConnection"] = "Add connection",
        ["RemoveConnection"] = "Remove connection",
        ["RenameConnection"] = "Rename connection",
        ["TestConnection"] = "Test connection",
        ["SignInConnection"] = "Sign in to connection",
        ["CreateMemory"] = "Create memory",
        ["WriteMemoryFact"] = "Write memory fact",
        ["ForgetMemoryFact"] = "Forget memory facts",
        ["PurgeResources"] = "Purge resources",
        ["CreateToolkit"] = "Create toolkit",
        ["BuildToolkit"] = "Build toolkit",
        ["StartToolkit"] = "Start toolkit",
        ["StopToolkit"] = "Stop toolkit",
        ["PauseToolkit"] = "Pause toolkit",
        ["ResumeToolkit"] = "Resume toolkit",
        ["CreateWorkflow"] = "Create workflow",
        ["ReplaceWorkflow"] = "Replace workflow",
        ["ApplyWorkflowOps"] = "Edit workflow",
        ["ValidateWorkflow"] = "Check workflow",
        ["ActivateWorkflow"] = "Activate workflow",
        ["PauseWorkflow"] = "Pause workflow",
        ["TestWorkflow"] = "Test run",
        ["PinWorkflowOutput"] = "Pin this output",
        ["DeleteWorkflow"] = "Archive workflow",
        ["OnboardRepo"] = "Onboard repo",
        ["InitProfile"] = "Create profile",
        ["SetProfileKey"] = "Set a profile key",
        ["SetReviewLogin"] = "Set review login",
        ["SkipSetupStep"] = "Skip a setup step",
        ["RestartProcess"] = "Restart Sindri",
        ["ConnectAccount"] = "Connect an account",
        ["ChangeLinearIssue"] = "Edit Linear issue",
        ["SendReviewFix"] = "Send review fix",
        ["CommentLinearIssue"] = "Comment on Linear issue",
        ["CreateLinearIssue"] = "Create Linear issue",
        ["ListLinearTeams"] = "List Linear teams",
        ["SaveLinearConnection"] = "Save Linear key",
        ["RemoveLinearWriteKey"] = "Remove Linear write key",
        ["AttachProject"] = "Add project",
        ["ChangeProject"] = "Edit project",
        ["AddTodo"] = "Add todo",
        ["ChangeTodo"] = "Edit todo",
        ["MarkTodo:done"] = "Check off todo",
        ["MarkTodo:reopen"] = "Reopen todo",
        ["MarkTodo:remove"] = "Remove todo",
        ["MarkTodo:restore"] = "Restore todo",
        ["KeepTodo"] = "Keep a version of a todo",
        ["KeepTodoUndo"] = "Undo keeping a version",
        ["PickTodoHold:apply"] = "Apply held note changes",
        ["PickTodoHold:restore"] = "Put held note changes back",
        ["UndoTodoHold"] = "Undo the held-changes pick",
        ["CheckTodoNote"] = "Check todo note",
        ["ImportTodoNote"] = "Import the todo note",
        ["SetTodoNote"] = "Set the todo note",
        ["StopTodoNote"] = "Stop syncing the todo note",
        ["EmbedCodeIndex"] = "Embed code index",
        ["BuildContextMap"] = "Build map",
        ["RefreshContextMap"] = "Refresh map",
        ["PinConcepts"] = "Pin concepts",
        ["SetPages"] = "Name pages",
        ["PublishContextMap"] = "Publish map",
        ["RelinkPinAliases"] = "Link records to pins",
        ["ScanContextConcepts"] = "Scan for concepts",
        ["RunContextBacktest"] = "Run map backtest",
        ["CancelJob"] = "Cancel job",
        ["AnswerQuestion"] = "Answer question",
        ["CancelWorkflowRun"] = "Cancel workflow run",
        ["RerunWorkflowRun"] = "Re-run workflow run from a step",
        ["TakeOverAgentNode"] = "Take over agent",
        ["HandBackAgentNode"] = "Hand back agent",
        ["RunWorkflow"] = "Run workflow",
        ["ResumeJob"] = "Resume job",
        ["CloseItem:cancelled"] = "Close as cancelled",
        ["CloseItem:done"] = "Close item",
        ["CompleteStep"] = "Complete",
        ["CompleteStep:report"] = "Report agent run",
        ["ConnectIndexSource"] = "Connect a source",
        ["CreateHandoffItem"] = "Hand off work",
        ["CreateItem"] = "Create item",
        ["Decide"] = "Decide",
        ["DetachProject"] = "Remove project",
        ["FileRequest"] = "File a request",
        ["ImportContextMap"] = "Import context map",
        ["IssueAppChallenge"] = "Ask for a confirmation",
        ["FocusSession"] = "Focus",
        ["HideProject"] = "Hide project",
        ["IssueChallenge"] = "Get approval challenge",
        ["IssueNonce"] = "Get command nonce",
        ["OpenItem:attach"] = "Attach",
        ["ShowLineHoldout"] = "Show hidden scenarios",
        ["SessionExit"] = "Report session exit",
        ["RecordLimitHit"] = "Record a sandbox limit hit",
        ["EndLearnMode"] = "End sandbox learn mode",
        ["AllowDenial"] = "Allow for this repo",
        ["SessionHeartbeat"] = "Report session heartbeat",
        ["OpenItem:open"] = "Open",
        ["OpenItem:resume"] = "Resume session",
        ["PauseService"] = "Pause service",
        ["LaunchInit"] = "Set up the launcher",
        ["LaunchShow"] = "Show a launch",
        ["ProbeLaunchTargets"] = "Check launch targets again",
        ["PromoteContextMap"] = "Promote map",
        ["CheckContextMapStructure"] = "Check map structure",
        ["PurgeIndexSource"] = "Purge index source",
        ["ProposeContextMap"] = "Propose map from scope",
        ["BuildCodeIndex"] = "Build code index",
        ["RebuildViews"] = "Rebuild views",
        ["RegisterMcp"] = "Connect agents",
        ["RegisterRecipe"] = "Register recipe",
        ["ResumeService"] = "Resume service",
        ["StartSession"] = "Start agent",
        ["RemoveSessionWorktree"] = "Remove agent worktree",
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
        ["StartSkillRun"] = "Run skill",
        ["StopProcess"] = "Stop Sindri",
        ["SyncIndexSource"] = "Sync index source",
        ["SyncSources"] = "Sync context sources",
        ["ValidateProfile"] = "Validate profile",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The Health page's catalog section.</summary>
    internal const string ActionsCard = "Actions";

    /// <summary>The copy button.</summary>
    internal const string Copy = "Copy";

    /// <summary>After a copy, for 1.5 s (§4.1, §12.4).</summary>
    internal const string Copied = "Copied";

    /// <summary>The button that copies a whole hash where the screen shows its first digits.</summary>
    internal const string CopyHash = "Copy hash";

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

    /// <summary>A spec'd phase that has not shipped: "arrives in U8b".</summary>
    /// <param name="phase">The phase.</param>
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
