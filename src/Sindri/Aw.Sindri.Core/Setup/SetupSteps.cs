using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The pure decision of each setup step (GET /sindri/setup): one function per step from the fact its source gave, and <see cref="Unreadable(SetupStepId, Error)"/> for a source that couldn't be read.
/// Titles, reasons and details are plain words; an error code appears only in <see cref="SetupError.Code"/>.
/// </summary>
public static class SetupSteps
{
    private const string RepoTitle = "Add your repo";
    private const string ProfileTitle = "Save the profile";
    private const string McpTitle = "Connect your agents";
    private const string MapTitle = "Build the map";
    private const string SourcesTitle = "Connect your accounts";
    private const string ServeTitle = "Sindri is current";
    private const string AgentTitle = "Agent for workflows";
    private const string FirstWorkflowTitle = "Run hello-sindri";

    /// <summary>The repo step: the owner's until the profile names a repo.</summary>
    /// <param name="fact">The repos.</param>
    /// <returns>The step.</returns>
    public static SetupStep Repo(RepoFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Count > 0
            ? new SetupStep(SetupStepId.Repo, RepoTitle, SetupState.Done, "A repo is in your profile.", Names(fact.Items, Counted(fact.Count, "repository", "repositories")), Error: null, Action: null, Progress: null) { Items = fact.Items.Lines }
            : new SetupStep(SetupStepId.Repo, RepoTitle, SetupState.NeedsYou, "Pick the folder of a repo to start.", Detail: null, Error: null, SetupActions.AddRepo, Progress: null);
    }

    /// <summary>The profile step: it waits for a repo, then done once the file is there (edits apply on save).</summary>
    /// <param name="fact">Where the profile stands.</param>
    /// <param name="repos">The repos (a profile with none is not ready, whatever the file says).</param>
    /// <returns>The step.</returns>
    public static SetupStep Profile(ProfileFact fact, RepoFact repos)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(repos);
        if (repos.Count == 0)
        {
            return new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Todo, "Waits for your first repo.", Detail: null, Error: null, Action: null, Progress: null);
        }

        var step = fact.Presence switch
        {
            ProfilePresence.Present => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Done, "Your profile is saved and live.", Detail: null, Error: null, Action: null, Progress: null),
            ProfilePresence.Missing => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Todo, "There is no profile yet.", Detail: null, Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Presence, "Not a profile presence"),
        };
        return step with { Preview = fact.Preview };
    }

    /// <summary>The MCP step: it needs an installed provider and a profile with a repo, then done when every repo is registered.</summary>
    /// <param name="fact">Where the registration stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Mcp(McpFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Providers.Length == 0)
        {
            return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Todo, "Install Claude Code, Codex or Cursor.", Detail: null, Error: null, Action: null, Progress: null);
        }

        if (fact.Repos == 0)
        {
            return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Todo, "Waits for a profile with a repo.", Detail: null, Error: null, Action: null, Progress: null);
        }

        if (fact.Registered >= fact.Repos)
        {
            return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Done, "Sindri is registered in your repos.", fact.Providers + " in " + Names(fact.Items, Counted(fact.Repos, "repository", "repositories")) + ".", Error: null, Action: null, Progress: null) { Items = fact.Items.Lines };
        }

        var detail = fact.Repos == 1
            ? "Your agent doesn't know where Sindri is yet."
            : string.Create(CultureInfo.InvariantCulture, $"Your agent knows where Sindri is in {fact.Registered} of {fact.Repos} repositories.");
        return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Todo, "Sindri is not registered in every repo yet.", detail, Error: null, SetupActions.RegisterMcp, fact.Repos == 1 ? null : new SetupProgress(fact.Registered, fact.Repos, "repos")) { Items = fact.Items.Lines };
    }

    /// <summary>The map step: it builds on its own, so it has no action.</summary>
    /// <param name="fact">Where the map stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Map(MapFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var repos = Names(fact.Items, "your repos");
        var step = fact.Phase switch
        {
            MapPhase.Built => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Done, "Every repo has a current map.", "The map is built.", Error: null, Action: null, Progress: null),
            MapPhase.Building when fact.Retry is { } retry => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Running, "Sindri is mapping " + repos + ".", RetryDetail(retry), Error: null, SetupActions.RefreshContextMap, fact.Progress) { Stage = fact.Stage },
            MapPhase.Building => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Running, "Sindri is mapping " + repos + ".",
                fact.Progress is { } p ? string.Create(CultureInfo.InvariantCulture, $"{p.Done} of {p.Total} done.") : "Reading the code.", Error: null, Action: null, fact.Progress)
            { Stage = fact.Stage },
            MapPhase.Failed => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Failed, "The last try at building the map didn't finish.", Detail: null,
                new SetupError(fact.FailureCode ?? "SND-CONTEXT-042", "The last try at building the map didn't finish."), Action: null, Progress: null)
            { Stage = fact.Stage },
            MapPhase.NotBuilt => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, "The map isn't built yet. Sindri builds it on its own.", Detail: null, Error: null, Action: null, Progress: null),
            MapPhase.Waiting => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, "Waits for a profile with a repo.", Detail: null, Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Phase, "Not a map phase"),
        };
        return step with { Items = fact.Items.Lines };
    }

    /// <summary>
    /// The accounts step (id `sources`, step 2 of first-run spec §3.1): done when every github, slack or linear source the profile names has its token and at least one is named; skipped when the owner skipped it and nothing is connected;
    /// otherwise the human's, with `sindri index connect`.
    /// </summary>
    /// <param name="fact">Where the sources stand.</param>
    /// <returns>The step.</returns>
    public static SetupStep Sources(SourcesFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var missing = fact.Items.Lines.Where(static i => i.State != SetupState.Done).Select(static i => SourceName(i.Repo)).ToList();
        if (fact.Items.Lines.Count > 0 && missing.Count == 0)
        {
            return new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Done, "Every source your profile names is connected.", string.Join(", ", fact.Items.Lines.Select(static i => SourceName(i.Repo))) + ".", Error: null, Action: null, Progress: null) { Items = fact.Items.Lines };
        }

        if (fact.Skipped)
        {
            return new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Skipped, "You skipped connecting accounts.", "Connect GitHub, Slack or Linear any time.", Error: null, Action: null, Progress: null) { Items = fact.Items.Lines };
        }

        return fact.Items.Lines.Count == 0
            ? new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Todo, "No source is connected yet.", "Connect GitHub, Slack or Linear so Sindri can read your issues and chat.", Error: null, SetupActions.ConnectAccount, Progress: null)
            : new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Todo, string.Join(" and ", missing) + (missing.Count == 1 ? " needs" : " need") + " a token.", Detail: null, Error: null, SetupActions.ConnectAccount, Progress: null) { Items = fact.Items.Lines };
    }

    /// <summary>The serve step (step 1): done when the running Sindri is the installed build; otherwise the owner's, with a restart.</summary>
    /// <param name="fact">Whether serve runs the installed build.</param>
    /// <returns>The step.</returns>
    public static SetupStep Serve(ServeFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return fact.Current
            ? new SetupStep(SetupStepId.Serve, ServeTitle, SetupState.Done, "Sindri is running the installed build.", Detail: null, Error: null, Action: null, Progress: null)
            : new SetupStep(SetupStepId.Serve, ServeTitle, SetupState.NeedsYou, "A different Sindri is installed than the one running.", "Restart Sindri to run the installed one.", Error: null, SetupActions.RestartProcess, Progress: null);
    }

    /// <summary>The agent step (step 5): it waits for a repo, then done once `workflow.agent.target` is set. It has no action: the setup service sets the target from the first agent it finds.</summary>
    /// <param name="fact">Which agent the workflows use.</param>
    /// <param name="repos">The repos (the setup service picks an agent once the profile names one).</param>
    /// <returns>The step.</returns>
    public static SetupStep Agent(AgentFact fact, RepoFact repos)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(repos);
        if (!string.IsNullOrWhiteSpace(fact.Target))
        {
            return new SetupStep(SetupStepId.Agent, AgentTitle, SetupState.Done, "Workflows run on an agent.", "Workflows run on " + fact.Target + ".", Error: null, Action: null, Progress: null);
        }

        return repos.Count == 0
            ? new SetupStep(SetupStepId.Agent, AgentTitle, SetupState.Todo, "Waits for your first repo.", Detail: null, Error: null, Action: null, Progress: null)
            : new SetupStep(SetupStepId.Agent, AgentTitle, SetupState.Todo, "Sindri picks the first agent it finds.", Detail: null, Error: null, Action: null, Progress: null);
    }

    /// <summary>The hello-sindri step (step 8): done after one successful run, skipped when the owner dismissed it, otherwise waiting. It has no action until the workflow exists (engine lane).</summary>
    /// <param name="fact">Where the run stands.</param>
    /// <param name="repos">The repos (hello-sindri is made once the profile names one).</param>
    /// <returns>The step.</returns>
    public static SetupStep FirstWorkflow(FirstWorkflowFact fact, RepoFact repos)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(repos);
        if (fact.Succeeded)
        {
            return new SetupStep(SetupStepId.FirstWorkflow, FirstWorkflowTitle, SetupState.Done, "hello-sindri ran once.", Detail: null, Error: null, Action: null, Progress: null);
        }

        if (fact.Dismissed)
        {
            return new SetupStep(SetupStepId.FirstWorkflow, FirstWorkflowTitle, SetupState.Skipped, "You dismissed this step.", "Run hello-sindri any time.", Error: null, Action: null, Progress: null);
        }

        return repos.Count == 0
            ? new SetupStep(SetupStepId.FirstWorkflow, FirstWorkflowTitle, SetupState.Todo, "Waits for your first repo.", Detail: null, Error: null, Action: null, Progress: null)
            : new SetupStep(SetupStepId.FirstWorkflow, FirstWorkflowTitle, SetupState.Todo, "hello-sindri has not run yet.", Detail: null, Error: null, Action: null, Progress: null);
    }

    /// <summary>The step for a source that couldn't be read: failed, with one plain sentence and the error's code.</summary>
    /// <param name="id">Which step.</param>
    /// <param name="code">The code of the error the read returned.</param>
    /// <returns>The step.</returns>
    public static SetupStep Unreadable(SetupStepId id, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var (title, sentence) = id switch
        {
            SetupStepId.Repo => (RepoTitle, "Sindri couldn't read your profile, so it can't tell which repositories are added."),
            SetupStepId.Profile => (ProfileTitle, "Sindri couldn't read your profile."),
            SetupStepId.Mcp => (McpTitle, "Sindri couldn't check whether your agent knows where it is."),
            SetupStepId.Map => (MapTitle, "Sindri couldn't read the state of the map."),
            SetupStepId.Sources => (SourcesTitle, "Sindri couldn't read which sources your profile names."),
            SetupStepId.Serve => (ServeTitle, "Sindri couldn't tell which build is running."),
            SetupStepId.Agent => (AgentTitle, "Sindri couldn't read which agent your workflows use."),
            SetupStepId.FirstWorkflow => (FirstWorkflowTitle, "Sindri couldn't read whether hello-sindri has run."),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not a setup step"),
        };
        return Failed(id, title, new SetupError(code, sentence));
    }

    /// <summary>
    /// The step for a source whose read failed with <paramref name="error"/> (#3437): like <see cref="Unreadable(SetupStepId, string)"/>, and a profile
    /// the source names as invalid (SND-PROFILE-002) puts its message, written for the owner, on the detail line: the key to fix, or "check its permissions".
    /// Any other error's message stays off the page.
    /// </summary>
    /// <param name="id">Which step.</param>
    /// <param name="error">The error the read returned.</param>
    /// <returns>The step.</returns>
    public static SetupStep Unreadable(SetupStepId id, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var step = Unreadable(id, error.Code);
        return string.Equals(error.Code, SindriErrors.ProfileInvalid.Code, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(error.Message)
            ? step with { Detail = Ended(error.Message.Trim()) }
            : step;
    }

    private static string Ended(string line) => line.EndsWith('.') ? line : line + ".";

    private static SetupStep Failed(SetupStepId id, string title, SetupError error) =>
        new(id, title, SetupState.Failed, error.Sentence, Detail: null, error, Action: null, Progress: null);

    private static string RetryDetail(MapRetry retry)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(retry.In.TotalMinutes));
        return $"Retrying in {Counted(minutes, "minute", "minutes")}: {retry.Reason}.";
    }

    private static string Names(SetupItems items, string fallback) => items.Lines.Count == 0 ? fallback : string.Join(", ", items.Lines.Select(static i => i.Repo));

    private static string SourceName(string source) => source switch
    {
        "github" => "GitHub",
        "slack" => "Slack",
        "linear" => "Linear",
        _ => source,
    };

    private static string Counted(int count, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many)}");
}
