using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The project calls of the fake (projects page spec §6.2): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _projectsRead = [];
    private readonly List<(string Route, object Body)> _projectWrites = [];
    private int _projectsCalls;
    private int _linearCalls;
    private int _attached;

    /// <summary>GET /sindri/projects's answer: the mixed golden by default.</summary>
    public RouteAnswer ProjectsAnswer { get; set; } = RouteAnswer.Api("projects", "mixed");

    /// <summary>GET /sindri/projects/linear's answer: the "some" golden by default.</summary>
    public RouteAnswer LinearAnswer { get; set; } = RouteAnswer.Api("projects-linear", "some");

    /// <summary>How many times the Linear project list was read.</summary>
    public int LinearCalls => Volatile.Read(ref _linearCalls);

    /// <summary>When set, the Linear project list read waits for it: the tab stays loading until the test completes it.</summary>
    public TaskCompletionSource? LinearGate { get; set; }

    /// <summary>GET /sindri/projects/{id}'s answer per project id; an id not here answers the attached golden.</summary>
    public Dictionary<string, RouteAnswer> ProjectAnswers { get; } = new(StringComparer.Ordinal);

    /// <summary>The answer of every project write (change, detach, hide); null answers an accepted write for the project, one version on.</summary>
    public RouteAnswer? ProjectWriteAnswer { get; set; }

    /// <summary>Runs after an accepted project write (route, body), as the projector would move the card (a test sets the next answers here).</summary>
    public Action<string, object>? OnProjectWritten { get; set; }

    /// <summary>Every project write received (route, body), in order.</summary>
    public IReadOnlyList<(string Route, object Body)> ProjectWrites { get { lock (_gate) { return [.. _projectWrites]; } } }

    /// <summary>How many times the project list was read.</summary>
    public int ProjectsCalls => Volatile.Read(ref _projectsCalls);

    /// <summary>The "all" flag of the latest list read; null before the first.</summary>
    public bool? LastProjectsAll { get; private set; }

    /// <summary>Every project id one card was read for, in order.</summary>
    public IReadOnlyList<string> ProjectsRead { get { lock (_gate) { return [.. _projectsRead]; } } }

    /// <summary>When set, the project list read waits for it: the page stays loading until the test completes it.</summary>
    public TaskCompletionSource? ProjectsGate { get; set; }

    public async Task<Result<ProjectList>> Projects(Uri api, bool all, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _projectsCalls);
        LastProjectsAll = all;
        if (ProjectsGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ProjectList>(api, ProjectRoutes.List(all), ProjectsAnswer);
    }

    public async Task<Result<ProjectLinearList>> LinearProjects(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _linearCalls);
        if (LinearGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Answer<ProjectLinearList>(api, ProjectRoutes.Linear, LinearAnswer);
    }

    public Task<Result<ProjectOne>> Project(Uri api, string id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _projectsRead.Add(id);
        }

        var answer = ProjectAnswers.TryGetValue(id, out var own) ? own : RouteAnswer.Api("projects-id", "attached");
        return Task.FromResult(Answer<ProjectOne>(api, ProjectRoutes.One(id), answer));
    }

    public Task<Result<ProjectAcceptedView>> ChangeProject(Uri api, string id, ProjectChange change, CancellationToken cancellationToken) =>
        ProjectWrite(api, ProjectRoutes.Change(id), change, id, change.Version, cancellationToken);

    public Task<Result<ProjectAcceptedView>> DetachProject(Uri api, string id, ProjectDetach detach, CancellationToken cancellationToken) =>
        ProjectWrite(api, ProjectRoutes.Detach(id), detach, id, detach.Version, cancellationToken);

    public Task<Result<ProjectAcceptedView>> HideProject(Uri api, ProjectHide hide, CancellationToken cancellationToken) =>
        ProjectWrite(api, ProjectRoutes.Hide, hide, hide.Linear, hide.Version, cancellationToken);

    public Task<Result<ProjectAcceptedView>> AttachProject(Uri api, ProjectAttach attach, CancellationToken cancellationToken) =>
        ProjectWrite(api, ProjectRoutes.Attach, attach, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"prj_{Interlocked.Increment(ref _attached):D26}"), version: 0, cancellationToken, attach.Name ?? attach.Linear);

    private async Task<Result<ProjectAcceptedView>> ProjectWrite(Uri api, string route, object body, string project, long version, CancellationToken cancellationToken, string? name = null)
    {
        if (NotReceived<ProjectAcceptedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _projectWrites.Add((route, body));
        }

        await WaitFor(CommandGate, cancellationToken).ConfigureAwait(false);
        var accepted = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $$"""{"accepted":{"commandId":"cmd_fake","sequence":41},"project":"{{project}}","version":{{version + 1}},"name":"{{name ?? project}}"}""");
        var answer = Answer<ProjectAcceptedView>(api, route, ProjectWriteAnswer ?? new RouteAnswer(System.Net.HttpStatusCode.OK, accepted));
        if (answer.IsSuccess)
        {
            _ = Interlocked.Exchange(ref _pending, answer.Value.Accepted.Sequence);
            OnProjectWritten?.Invoke(route, body);
        }

        return answer;
    }
}
