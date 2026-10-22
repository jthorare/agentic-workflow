using System.Collections.Frozen;
using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the setup checklist, the first section of Settings, and Home's one line (setup spec §6). Plain words: a step is a step, a recipe is a workflow, a code waits in Details.</summary>
internal static partial class Labels
{
    // A step's action by its catalog id: the words before it is done, and the words once it is ("another", "again").
    private static readonly FrozenDictionary<string, (string First, string Again)> _setupActions = new Dictionary<string, (string First, string Again)>(StringComparer.Ordinal)
    {
        ["AddRepo"] = ("Add a repo", "Add another repo"),
        ["ApproveProfile"] = ("Review and approve", "Review and approve"),
        ["ApproveAllRecipes"] = ("Approve all workflows", "Approve again"),
        ["RegisterMcp"] = ("Connect your agents", "Connect again"),
        ["SetupOnyx"] = ("Set up Onyx", "Set up again"),
        ["RefreshContextMap"] = ("Retry", "Retry"),
        ["ConnectOnyx"] = ("Connect", "Connect"),
        ["DeferOnyx"] = ("Not now", "Not now"),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The app's titles, keyed by step id: the API's own are its plain default, the app owns the words people read.
    private static readonly FrozenDictionary<string, string> _setupTitles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["repo"] = "Add your repository",
        ["profile"] = "Approve your settings",
        ["recipes"] = "Approve the workflows",
        ["mcp"] = "Connect your coding agents",
        ["map"] = "Build the product map",
        ["onyx"] = "Connect Linear and Slack (Onyx)",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The checklist's lead while a step is open.</summary>
    internal const string SetupLead = "Set up Sindri";

    /// <summary>The lead once every step is done or skipped.</summary>
    internal const string SetupLeadDone = "Sindri is set up";

    /// <summary>The one line under the lead (hidden once setup is finished).</summary>
    internal const string SetupSubline = "Six steps. Sindri does most of them for you; you decide the ones that give it access.";

    /// <summary>The line that says what the person already ran.</summary>
    internal const string SetupRunLine = "You've run";

    /// <summary>The command that line names.</summary>
    internal const string SetupRunCommand = "./setup.sh --with-sindri";

    /// <summary>Home's one line while any step is open.</summary>
    internal const string SetupHomeLine = "Finish setting up Sindri";

    /// <summary>Home's control that opens Settings at the first step still to do.</summary>
    internal const string SetupContinue = "Continue";

    /// <summary>The step Home's line points at, as its name reads it.</summary>
    internal static string SetupNext(string step) => "Next: " + step;

    /// <summary>The checklist before the first answer.</summary>
    internal const string ReadingSetup = "Reading setup…";

    /// <summary>When Sindri has no setup list (an older Sindri).</summary>
    internal const string SetupNotServed = "Setup isn't available in this build yet.";

    /// <summary>When the answer can't be read.</summary>
    internal const string SetupUnreadable = "The setup list can't be read from this Sindri.";

    /// <summary>When the first read failed: the reason follows.</summary>
    internal static string SetupFailed(Error error) => "Couldn't read setup just now. " + InstanceRefused(error);

    /// <summary>The line over the launcher command.</summary>
    internal const string SetupStartIt = "Start it from a terminal:";

    /// <summary>What starting it does for the app.</summary>
    internal const string SetupReconnects = "The app reconnects on its own when Sindri starts.";

    /// <summary>The launcher command the daily path uses.</summary>
    internal const string SetupLauncher = "sindri-app";

    /// <summary>The disclosure that holds ids and codes.</summary>
    internal const string SetupDetails = "Details";

    /// <summary>The hint over a copyable command.</summary>
    internal const string SetupCliHint = "Or run this in a terminal:";

    /// <summary>The Details label of a step's id.</summary>
    internal const string SetupStepField = "Step";

    /// <summary>The Details label of a step's state as served.</summary>
    internal const string SetupStateField = "State";

    /// <summary>The Details label of the action id.</summary>
    internal const string SetupActionField = "Action";

    /// <summary>The map's note after Retry started a new build.</summary>
    internal const string SetupMapAgain = "Building the map again.";

    /// <summary>The map's note when a build was already running (the 409 with its running job).</summary>
    internal const string SetupMapAlready = "Already building the map.";

    /// <summary>The map's note when Retry could not start one.</summary>
    internal const string SetupMapRetryFailed = "Couldn't start the map again. Try again in a moment.";

    /// <summary>"2 of 6 done", or "All 6 done".</summary>
    internal static string SetupCount(int done, int total) =>
        done == total && total > 0 ? string.Create(CultureInfo.InvariantCulture, $"All {total} done") : string.Create(CultureInfo.InvariantCulture, $"{done} of {total} done");

    /// <summary>"40 of 100 files" for a counted step ("40 of 100" when the server names no unit).</summary>
    internal static string SetupProgress(int done, int total, string? unit = null) =>
        string.IsNullOrWhiteSpace(unit) ? string.Create(CultureInfo.InvariantCulture, $"{done} of {total}") : string.Create(CultureInfo.InvariantCulture, $"{done} of {total} {unit}");

    /// <summary>The app's title for a step; a step this build doesn't know keeps the API's.</summary>
    /// <param name="id">The step's id.</param>
    /// <param name="served">The API's own title.</param>
    /// <returns>The title.</returns>
    internal static string SetupTitle(string id, string served) => _setupTitles.TryGetValue(id, out var title) ? title : served;

    /// <summary>A step's state as the chip draws it: one of five words with its tone. A word this build doesn't know shows as itself on the To do chip.</summary>
    /// <param name="state">The state as served.</param>
    /// <returns>The chip's word, the word the row says, and the tone.</returns>
    internal static (string Word, string Said, ChipTone Tone) SetupChip(string state) => state switch
    {
        "needs-you" => ("Needs you", "Needs you", ChipTone.Attn),
        "failed" => ("Failed", "Failed", ChipTone.Fail),
        "running" => ("Working", "Working", ChipTone.Neutral),
        "done" => ("Done", "Done", ChipTone.Ok),
        "skipped" => ("Skipped", "Skipped", ChipTone.Neutral),
        "todo" => ("To do", "To do", ChipTone.Neutral),
        _ => (state, "To do", ChipTone.Neutral),
    };

    /// <summary>The line under a step's title: the app's own words for the state the step is in, or what it did once it is done; the API's sentence for a state the app has no words for.</summary>
    /// <param name="id">The step's id.</param>
    /// <param name="state">The state as served.</param>
    /// <param name="waiting">True for a "todo" step with no action: it waits on another step.</param>
    /// <param name="served">The API's sentence.</param>
    /// <param name="detail">What the step did or is doing, or null.</param>
    /// <returns>The line.</returns>
    internal static string SetupSubFor(string id, string state, bool waiting, string served, string? detail) => (id, state) switch
    {
        (_, "done" or "skipped") when !string.IsNullOrEmpty(detail) => detail,
        ("repo", "todo" or "needs-you") => "Sindri works inside a repository you choose. Pick its top folder.",
        ("profile", "todo") when waiting => "Waits for a repository.",
        ("profile", "needs-you") => "Your settings changed. Sindri works only inside what you approve here.",
        ("profile", "done") => "Sindri works only inside what you approved.",
        ("recipes", "todo") when waiting => "Waits for your settings.",
        ("recipes", "needs-you") => "The workflows are loaded and waiting. Read what each may do, then approve them all at once.",
        ("recipes", "done") => "All the workflows can run.",
        ("mcp", "todo") when waiting => "Waits for your settings.",
        ("mcp", "running") => "Sindri is adding itself to this repository for Claude Code, Codex and Cursor.",
        ("mcp", "done") => "Claude Code, Codex and Cursor can reach Sindri in this repository.",
        ("map", "todo") when waiting => "Waits for your settings.",
        ("map", "running") => "Sindri reads your code and builds a map of it. It builds itself and keeps up to date.",
        ("map", "done") => "The map is up to date with your code.",
        ("map", "failed") => "The map could not be built.",
        ("onyx", "todo") => "Onyx lets Sindri read your tickets and chats. Sign in once.",
        ("onyx", "needs-you") => "Sign in to Onyx once. Your password is used for this step only.",
        ("onyx", "done") => "Sindri can read your Linear and Slack context.",
        _ => served,
    };

    /// <summary>What the live region says when a step changes: "Approve the workflows: done".</summary>
    internal static string SetupChanged(string title, string word) => title + ": " + word.ToLowerInvariant();

    /// <summary>A step row's accessible name: its title, its state and what is under it.</summary>
    internal static string SetupStepName(string title, string word, string sub) => title + ", " + word.ToLowerInvariant() + ". " + sub;

    /// <summary>The words of a step's action. A step that is already done offers it again, or another.</summary>
    /// <param name="action">The catalog action id.</param>
    /// <param name="done">True when the step is done.</param>
    /// <returns>The words.</returns>
    internal static string SetupActionLabel(string action, bool done) =>
        _setupActions.TryGetValue(action, out var words) ? (done ? words.Again : words.First) : Action(action);

    /// <summary>The terminal form of a step's action when the catalog serves none.</summary>
    /// <param name="action">The catalog action id.</param>
    /// <returns>The command, or empty for an action this build doesn't know.</returns>
    internal static string SetupCli(string action) => action switch
    {
        "AddRepo" => "sindri-app repo add <path>",
        "ApproveProfile" => "sindri-app profile approve <hash>",
        "ApproveAllRecipes" => "sindri-app recipe approve-all",
        "RegisterMcp" => "sindri-app mcp register --apply",
        "SetupOnyx" => "sindri-app onyx up",
        "RefreshContextMap" => "sindri context map refresh",
        _ => string.Empty,
    };
}
