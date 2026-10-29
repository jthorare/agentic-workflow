using Aw.Kernel;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The project calls of the fake (projects page spec §6.2): each answers the API's goldens, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<string> _projectsRead = [];
    private int _projectsCalls;

    /// <summary>GET /sindri/projects's answer: the mixed golden by default.</summary>
    public RouteAnswer ProjectsAnswer { get; set; } = RouteAnswer.Api("projects", "mixed");

    /// <summary>GET /sindri/projects/{id}'s answer per project id; an id not here answers the attached golden.</summary>
    public Dictionary<string, RouteAnswer> ProjectAnswers { get; } = new(StringComparer.Ordinal);

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

    public Task<Result<ProjectOne>> Project(Uri api, string id, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _projectsRead.Add(id);
        }

        var answer = ProjectAnswers.TryGetValue(id, out var own) ? own : RouteAnswer.Api("projects-id", "attached");
        return Task.FromResult(Answer<ProjectOne>(api, ProjectRoutes.One(id), answer));
    }
}
