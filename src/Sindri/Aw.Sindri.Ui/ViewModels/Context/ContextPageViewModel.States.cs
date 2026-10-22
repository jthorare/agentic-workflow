using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Jobs;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The states of the stage (F3 spec 1 §3): No map (the served answer, with a button that builds it) and Building (a build job runs). A refusal that is neither stays the banner.</summary>
internal sealed partial class ContextPageViewModel
{
    private const string NoMapCode = "SND-CONTEXT-030";
    private const string BuildAction = "BuildContextMap";
    private const string JobConflict = "SND-JOB-409";
    private const string Running = "running";
    private string? _buildJob;

    /// <summary>The Building state's title.</summary>
    public string BuildingTitle { get; } = Labels.ContextStateBuilding;

    /// <summary>The Building state's body.</summary>
    public string BuildingBody { get; } = Labels.ContextStateBuildingBody;

    /// <summary>The No map state's title.</summary>
    public string EmptyTitle { get; } = Labels.ContextStateEmptyTitle;

    /// <summary>The No map state's body.</summary>
    public string EmptyBody { get; } = Labels.ContextStateEmptyBody;

    /// <summary>The build button (the catalog's BuildContextMap), or null before the first no-map answer.</summary>
    public ControlViewModel? BuildControl { get; private set; }

    /// <summary>Whether the last read was answered "no map yet".</summary>
    internal bool NoMap { get; private set; }

    /// <summary>Whether a map build is running.</summary>
    internal bool IsBuilding { get; private set; }

    /// <summary>Which overlay covers the stage.</summary>
    internal ContextOverlay Overlay => ContextStates.Overlay(NoMap, IsBuilding);

    /// <summary>Whether the No map overlay shows (the page puts it on the map view only).</summary>
    public bool ShowEmpty => Overlay == ContextOverlay.Empty;

    /// <summary>Whether the Building overlay shows (the page puts it on the map view only).</summary>
    public bool ShowBuilding => Overlay == ContextOverlay.Building;

    // Registered once: after every event the running build is looked at, so the overlay goes when the map is there.
    private void InitStates() => Ports.Follow(FollowBuild);

    // A refusal is the No map state when the API says so; a build already running then shows as Building at once.
    private async Task NoteRefusal(Uri api, Error error, CancellationToken cancellationToken)
    {
        NoMap = string.Equals(error.Code, NoMapCode, StringComparison.Ordinal);
        if (!NoMap)
        {
            ChangedStates();
            return;
        }

        Problem = string.Empty;   // the overlay says it, not the banner
        BuildControl ??= MakeBuildControl();   // null until the catalog is read; the next no-map answer tries again
        if (!IsBuilding && await BuildRunning(api, cancellationToken).ConfigureAwait(true))
        {
            IsBuilding = true;
        }

        ChangedStates();
    }

    // A read that found a map ends the No map state; a build that is still running keeps its overlay until its job ends.
    private void NoteRead()
    {
        NoMap = false;
        ChangedStates();
    }

    // Readiness always comes from the catalog: until its BuildContextMap row is read there is no button.
    private ControlViewModel? MakeBuildControl()
    {
        Func<CancellationToken, Task<Result<CommandAcceptedView>>> send = StartBuild;
        return Ports.Catalog.On(BuildAction, subject: string.Empty, send);
    }

    // One press starts one build; a build that is already running is the state the page wanted anyway.
    private async Task<Result<CommandAcceptedView>> StartBuild(CancellationToken cancellationToken)
    {
        if (Ports.ApiUrl() is not { } api)
        {
            return SindriUiErrors.Unreachable.With("Sindri isn't reachable yet");
        }

        var started = await Ports.Api.BuildContextMap(api, cancellationToken).ConfigureAwait(true);
        if (started.IsSuccess)
        {
            _buildJob = started.Value.JobId;
            IsBuilding = true;
        }
        else if (RunningJob(started.Error) is { } running)
        {
            _buildJob = running;   // a build is already going: that is the state the page wanted
            IsBuilding = true;
        }

        ChangedStates();
        return started.Map(s => s.Accepted);
    }

    // Only a 409 that names the running job means a build is going; the job ended otherwise.
    private static string? RunningJob(Error error) => string.Equals(error.Code, JobConflict, StringComparison.Ordinal) && error.Extensions is { } extensions
        ? extensions.Where(e => string.Equals(e.Key, "runningJob", StringComparison.Ordinal)).Select(e => e.Value.GetString()).FirstOrDefault()
        : null;

    private async Task<bool> BuildRunning(Uri api, CancellationToken cancellationToken)
    {
        var jobs = await Ports.Api.Jobs(api, new JobQuery(Running, BuildAction), cancellationToken).ConfigureAwait(true);
        return jobs.IsSuccess && jobs.Value.Jobs.Any(j => string.Equals(j.Kind, BuildAction, StringComparison.Ordinal) && string.Equals(j.State, Running, StringComparison.Ordinal));
    }

    // Looks at the build this page started (or found running); when it has ended the map is read again, and the overlay goes with the answer.
    private async Task FollowBuild(Uri api, CancellationToken cancellationToken)
    {
        if (!IsBuilding)
        {
            return;
        }

        var ended = _buildJob is { } id
            ? await Ports.Api.Job(api, id, cancellationToken).ConfigureAwait(true) is { IsSuccess: true } job && !string.Equals(job.Value.State, Running, StringComparison.Ordinal)
            : !await BuildRunning(api, cancellationToken).ConfigureAwait(true);
        if (!ended)
        {
            return;
        }

        (IsBuilding, _buildJob) = (false, null);
        NoMap = false;
        ChangedStates();
        if (AnchorText.Length > 0)
        {
            await Submit(cancellationToken).ConfigureAwait(true);
        }
    }

    private void ChangedStates()
    {
        OnChanged(nameof(NoMap));
        OnChanged(nameof(IsBuilding));
        OnChanged(nameof(Overlay));
        OnChanged(nameof(ShowEmpty));
        OnChanged(nameof(ShowBuilding));
        OnChanged(nameof(BuildControl));
        OnChanged(nameof(HasProblem));
        OnChanged(nameof(Problem));
    }
}
