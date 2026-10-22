using System.Globalization;
using Aw.Sindri.Core.Onyx;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The pure decision of each setup step (GET /sindri/setup): one function per step from the fact its source gave, and <see cref="Unreadable"/> for a source that couldn't be read.
/// Titles, reasons and details are plain words; an error code appears only in <see cref="SetupError.Code"/>.
/// </summary>
public static class SetupSteps
{
    private const string RepoTitle = "Add your repo";
    private const string ProfileTitle = "Approve the profile";
    private const string RecipesTitle = "Approve the workflows";
    private const string McpTitle = "Connect your agents";
    private const string MapTitle = "Build the map";
    private const string OnyxTitle = "Set up Onyx";
    private const string DockerLink = "https://docs.docker.com/get-docker/";

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

    /// <summary>The profile step: it waits for a repo, then done when approved, the human's when it is waiting or has drifted.</summary>
    /// <param name="fact">Where the profile stands.</param>
    /// <param name="repos">The repos (a profile with none is not ready to approve, whatever the file says).</param>
    /// <returns>The step.</returns>
    public static SetupStep Profile(ProfileFact fact, RepoFact repos)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(repos);
        if (repos.Count == 0)
        {
            return new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Todo, "Waits for your first repo.", Detail: null, Error: null, Action: null, Progress: null);
        }

        var added = repos.Items.Lines.Count > 0 ? "You added " + string.Join(", ", repos.Items.Lines.Select(static i => i.Repo)) + ". " : string.Empty;
        var step = fact.Presence switch
        {
            ProfilePresence.Approved => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Done, "Your profile is approved.", Detail: null, Error: null, Action: null, Progress: null),
            ProfilePresence.Unapproved => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.NeedsYou, added + "Sindri waits for you to approve the profile before it uses it.", Detail: null, Error: null, SetupActions.ApproveProfile, Progress: null),
            ProfilePresence.Drifted => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.NeedsYou, "Your profile changed since you approved it. Read what changed and approve it again.", Detail: null, Error: null, SetupActions.ApproveProfile, Progress: null),
            ProfilePresence.Missing => new SetupStep(SetupStepId.Profile, ProfileTitle, SetupState.Todo, "There is no profile yet.", Detail: null, Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Presence, "Not a profile presence"),
        };
        return step with { Preview = fact.Preview };
    }

    /// <summary>The recipes step: it waits for an approved profile, then done when every recipe is approved, the human's while some wait.</summary>
    /// <param name="fact">Where the recipes stand.</param>
    /// <param name="profileApproved">Whether a profile has been approved.</param>
    /// <returns>The step.</returns>
    public static SetupStep Recipes(RecipesFact fact, bool profileApproved)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (fact.Total == 0)
        {
            return new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.Todo, "No workflows are registered yet. Sindri registers them when it starts.", Detail: null, Error: null, Action: null, Progress: null);
        }

        if (!profileApproved)
        {
            return new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.Todo, "After the profile is approved.", Counted(fact.Total, "workflow", "workflows") + " registered.", Error: null, Action: null, Progress: null);
        }

        var progress = new SetupProgress(fact.Approved, fact.Total, "workflows");
        var waiting = fact.Total - fact.Approved;
        return waiting <= 0
            ? new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.Done, "Every workflow that ships with Sindri is approved.", Counted(fact.Total, "workflow", "workflows") + ".", Error: null, Action: null, progress)
            : new SetupStep(SetupStepId.Recipes, RecipesTitle, SetupState.NeedsYou, Counted(waiting, "workflow", "workflows") + (waiting == 1 ? " waits" : " wait") + " for your approval.",
                string.Create(CultureInfo.InvariantCulture, $"Approved {fact.Approved} of {fact.Total}."), Error: null, SetupActions.ApproveAllRecipes, progress);
    }

    /// <summary>The MCP step: it needs an installed provider and an approved profile with a repo, then done when every repo is registered.</summary>
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
            return new SetupStep(SetupStepId.Mcp, McpTitle, SetupState.Todo, "Waits for an approved profile with a repo.", Detail: null, Error: null, Action: null, Progress: null);
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
            MapPhase.Building when fact.Retry is { } retry => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Running, "Sindri is mapping " + repos + ".", RetryDetail(retry), Error: null, SetupActions.RefreshContextMap, fact.Progress),
            MapPhase.Building => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Running, "Sindri is mapping " + repos + ".",
                fact.Progress is { } p ? string.Create(CultureInfo.InvariantCulture, $"{p.Done} of {p.Total} done.") : "Reading the code.", Error: null, Action: null, fact.Progress),
            MapPhase.Failed => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Failed, "The last try at building the map didn't finish.", Detail: null,
                new SetupError(fact.FailureCode ?? "SND-CONTEXT-042", "The last try at building the map didn't finish."), Action: null, Progress: null),
            MapPhase.NotBuilt => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, "The map isn't built yet. Sindri builds it on its own.", Detail: null, Error: null, Action: null, Progress: null),
            MapPhase.Waiting => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, "Waits for an approved profile with a repo.", Detail: null, Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Phase, "Not a map phase"),
        };
        return step with { Items = fact.Items.Lines };
    }

    /// <summary>The Onyx step: done when the pinned Onyx answers; skipped when this machine can't run it (no Docker, or a failed check); failed when it runs and doesn't answer or Docker can't be asked.</summary>
    /// <param name="fact">Where Onyx stands.</param>
    /// <returns>The step.</returns>
    public static SetupStep Onyx(OnyxFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        if (OnyxJobStep(fact) is { } job)
        {
            return job;
        }

        if (fact.State == OnyxState.Running)
        {
            return fact.Unreachable
                ? new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Failed, OnyxSentence("SND-ONYX-005"), Detail: null, new SetupError("SND-ONYX-005", OnyxSentence("SND-ONYX-005")), Action: null, Progress: null)
                : new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Done, "Onyx is running and Sindri can search it.", "Onyx is up and answering.", Error: null, Action: null, Progress: null);
        }

        if (fact.State == OnyxState.Unknown && fact.BlockedByCode is null)
        {
            // Every preflight check passed, yet Docker could not say whether Onyx runs: that is a failed read, not a missing Docker.
            return new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Failed, "Sindri couldn't ask Docker about Onyx.", Detail: null, new SetupError("SND-ONYX-011", "Sindri couldn't ask Docker about Onyx."), Action: null, Progress: null);
        }

        if (fact.BlockedByCode is not null)
        {
            var code = fact.BlockedByCode;
            return code == "SND-ONYX-001"
                ? new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Skipped, "Docker isn't installed.", "Install Docker to set up Onyx: " + DockerLink, Error: null, Action: null, Progress: null)
                : new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Skipped, OnyxSentence(code), "Fix that and Sindri will offer Onyx again.", Error: null, Action: null, Progress: null);
        }

        if (fact.Deferred)
        {
            return new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Skipped, "You chose to set up Onyx later.", "Onyx isn't set up. Use Set up Onyx in the card's menu when you want search.", Error: null, SetupActions.SetupOnyx, Progress: null);
        }

        return new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Todo, "Docker is ready. Set up Onyx when you want search.",
            fact.State == OnyxState.Stopped ? "Onyx is set up but stopped." : "Onyx isn't set up yet.", Error: null, SetupActions.SetupOnyx, Progress: null);
    }

    // The Onyx setup job's own states win over what Docker shows: the stack is up while the job still waits for the owner's login, and that is not "done".
    private static SetupStep? OnyxJobStep(OnyxFact fact) => fact.Job switch
    {
        OnyxJobPhase.Working => new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Running, "Sindri is setting up Onyx.", fact.JobLine ?? "Starting Onyx.", Error: null, Action: null, Progress: null),
        OnyxJobPhase.WaitingForLogin => new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.NeedsYou, "Create your Onyx account in the browser, then enter your login here.",
            "Sign up at " + OnyxSetupJob.SignupUrl(fact.Port), Error: null, SetupActions.ConnectOnyx, Progress: null),
        OnyxJobPhase.Failed when fact.JobFailure is { } failure => new SetupStep(SetupStepId.Onyx, OnyxTitle, SetupState.Failed, failure.Sentence, Detail: null, new SetupError(failure.Code, failure.Sentence), SetupActions.SetupOnyx, Progress: null),
        OnyxJobPhase.None or OnyxJobPhase.Failed => null,
        _ => null,
    };

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
            SetupStepId.Profile => (ProfileTitle, "Sindri couldn't read your profile or its approval."),
            SetupStepId.Recipes => (RecipesTitle, "Sindri couldn't read the list of workflows."),
            SetupStepId.Mcp => (McpTitle, "Sindri couldn't check whether your agent knows where it is."),
            SetupStepId.Map => (MapTitle, "Sindri couldn't read the state of the map."),
            SetupStepId.Onyx => (OnyxTitle, "Sindri couldn't check on Onyx."),
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Not a setup step"),
        };
        return Failed(id, title, new SetupError(code, sentence));
    }

    private static SetupStep Failed(SetupStepId id, string title, SetupError error) =>
        new(id, title, SetupState.Failed, error.Sentence, Detail: null, error, Action: null, Progress: null);

    private static string RetryDetail(MapRetry retry)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(retry.In.TotalMinutes));
        return $"Retrying in {Counted(minutes, "minute", "minutes")}: {retry.Reason}.";
    }

    private static string Names(SetupItems items, string fallback) => items.Lines.Count == 0 ? fallback : string.Join(", ", items.Lines.Select(static i => i.Repo));

    private static string OnyxSentence(string code) => code switch
    {
        "SND-ONYX-001" => "Docker isn't installed or isn't running, and Onyx runs in Docker.",
        "SND-ONYX-002" => "Docker Compose isn't available, and Onyx needs it.",
        "SND-ONYX-003" => "There isn't enough free memory or disk space to run Onyx.",
        "SND-ONYX-005" => "Onyx is running but isn't answering.",
        "SND-ONYX-006" => "The port Onyx needs is taken by something else.",
        "SND-ONYX-010" => "Docker points at another machine, and Sindri only runs Onyx on this one.",
        _ => "Onyx can't be set up on this machine right now.",
    };

    private static string Counted(int count, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many)}");
}
