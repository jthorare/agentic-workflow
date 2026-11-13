using System.Globalization;

namespace Aw.Sindri.Core.Setup;

/// <summary>
/// The pure decision of each setup step (GET /sindri/setup): one function per step from the fact its source gave, and <see cref="Unreadable"/> for a source that couldn't be read.
/// Titles, reasons and details are plain words; an error code appears only in <see cref="SetupError.Code"/>.
/// </summary>
public static class SetupSteps
{
    private const string RepoTitle = "Add your repo";
    private const string ProfileTitle = "Save the profile";
    private const string McpTitle = "Connect your agents";
    private const string MapTitle = "Build the map";
    private const string SourcesTitle = "Connect your sources";

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
                fact.Progress is { } p ? string.Create(CultureInfo.InvariantCulture, $"{p.Done} of {p.Total} done.") : "Reading the code.", Error: null, Action: null, fact.Progress) { Stage = fact.Stage },
            MapPhase.Failed => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Failed, "The last try at building the map didn't finish.", Detail: null,
                new SetupError(fact.FailureCode ?? "SND-CONTEXT-042", "The last try at building the map didn't finish."), Action: null, Progress: null),
            MapPhase.NotBuilt => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, "The map isn't built yet. Sindri builds it on its own.", Detail: null, Error: null, Action: null, Progress: null),
            MapPhase.Waiting => new SetupStep(SetupStepId.Map, MapTitle, SetupState.Todo, "Waits for a profile with a repo.", Detail: null, Error: null, Action: null, Progress: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fact), fact.Phase, "Not a map phase"),
        };
        return step with { Items = fact.Items.Lines };
    }

    /// <summary>The sources step: done when every github, slack or linear source the profile names has its token and at least one is named; otherwise the human's, with `sindri index connect`.</summary>
    /// <param name="fact">Where the sources stand.</param>
    /// <returns>The step.</returns>
    public static SetupStep Sources(SourcesFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var missing = fact.Items.Lines.Where(static i => i.State != SetupState.Done).Select(static i => SourceName(i.Repo)).ToList();
        if (fact.Items.Lines.Count == 0)
        {
            return new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Todo, "No source is connected yet.", "Connect GitHub, Slack or Linear so Sindri can read your issues and chat.", Error: null, SetupActions.ConnectIndexSource, Progress: null);
        }

        return missing.Count == 0
            ? new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Done, "Every source your profile names is connected.", string.Join(", ", fact.Items.Lines.Select(static i => SourceName(i.Repo))) + ".", Error: null, Action: null, Progress: null) { Items = fact.Items.Lines }
            : new SetupStep(SetupStepId.Sources, SourcesTitle, SetupState.Todo, string.Join(" and ", missing) + (missing.Count == 1 ? " needs" : " need") + " a token.", Detail: null, Error: null, SetupActions.ConnectIndexSource, Progress: null) { Items = fact.Items.Lines };
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

    private static string SourceName(string source) => source switch
    {
        "github" => "GitHub",
        "slack" => "Slack",
        "linear" => "Linear",
        _ => source,
    };

    private static string Counted(int count, string one, string many) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? one : many)}");
}
