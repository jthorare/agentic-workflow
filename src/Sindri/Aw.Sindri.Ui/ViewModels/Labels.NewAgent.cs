using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the New agent form (agents spec §15.4). Never a served sentence.</summary>
internal static partial class Labels
{
    internal const string NewAgentStart = "Start (Ctrl+Enter)";

    internal const string NewAgentStarting = "Starting...";

    internal const string NewAgentMakingWorktree = "Making the worktree";

    internal const string NewAgentNotOnboarded = "not onboarded";

    internal const string NewAgentShowIt = "Show it";

    internal const string NewAgentCopyCommand = "Copy command";

    internal const string NewAgentRetry = "Retry";

    internal const string NewAgentHeading = "New agent";

    internal const string NewAgentPromptLabel = "Prompt";

    internal const string NewAgentRepoLabel = "Repo";

    internal const string NewAgentAgentLabel = "Agent";

    internal const string NewAgentWorktreeLabel = "New worktree";

    internal const string NewAgentEsc = "Esc";

    /// <summary>The longest prompt the route takes.</summary>
    internal const int NewAgentPromptLimit = 8000;

    internal const string NewAgentRepoFallback = "That repo isn't available to start an agent in.";

    internal const string NewAgentAgentFallback = "That agent isn't installed or isn't signed in.";

    internal const string NewAgentFootFallback = "Couldn't start the agent. Nothing was left behind.";

    internal static string NewAgentStarted(string agent, string repo) => $"Started {agent} in {repo}.";

    internal static string NewAgentPromptTooLong(int count) =>
        string.Create(CultureInfo.InvariantCulture, $"The prompt is {count:N0} characters; the limit is {NewAgentPromptLimit:N0}.");

    internal static string NewAgentCount(int count) => string.Create(CultureInfo.InvariantCulture, $"{count:N0} / {NewAgentPromptLimit:N0}");
}
