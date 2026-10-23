using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Launch.T3;

/// <summary>A T3 Code in memory for the T3 host: projects by folder, threads by id, and a switch that makes it unreachable.</summary>
internal sealed class FakeT3Api : IT3Api
{
    private readonly List<T3Project> _projects = [];
    private readonly Dictionary<string, (string Status, bool Archived)> _threads = new(StringComparer.Ordinal);
    private readonly List<T3LaunchRequest> _launched = [];
    private readonly List<string> _interrupted = [];
    private readonly Dictionary<string, T3Model> _models = new(StringComparer.Ordinal) { ["claudeAgent"] = new T3Model("claudeAgent", "claude-test") };

    /// <summary>The environment id the fake reports.</summary>
    public string EnvironmentId { get; } = "env-1";

    /// <summary>When true, every call fails as an unreachable T3 does (SND-LAUNCH-025).</summary>
    public bool Down { get; set; }

    /// <summary>Every thread launched, in order.</summary>
    public IReadOnlyList<T3LaunchRequest> Launched => _launched;

    /// <summary>Every thread interrupted, in order.</summary>
    public IReadOnlyList<string> Interrupted => _interrupted;

    /// <summary>Adds a project rooted at <paramref name="root"/>.</summary>
    /// <param name="root">Its folder.</param>
    /// <param name="id">Its id.</param>
    /// <param name="repositoryKey">The repository T3 knows it as, or null.</param>
    public void AddProject(string root, string id = "project-1", string? repositoryKey = null) => _projects.Add(new T3Project(id, root, repositoryKey));

    /// <summary>Disables every provider instance of <paramref name="driverKind"/>, as T3's settings do.</summary>
    /// <param name="driverKind">The driver.</param>
    public void Disable(string driverKind) => _models.Remove(driverKind);

    /// <summary>Archives a thread, as the user does in the app.</summary>
    /// <param name="threadId">The thread.</param>
    public void Archive(string threadId) => _threads[threadId] = (_threads[threadId].Status, true);

    /// <summary>Deletes a thread.</summary>
    /// <param name="threadId">The thread.</param>
    public void Delete(string threadId) => _threads.Remove(threadId);

    public ValueTask<Result<T3Environment>> Environment(CancellationToken cancellationToken) =>
        Answer(() => (Result<T3Environment>)new T3Environment(EnvironmentId, "0.0.0-test"));

    public ValueTask<Result<IReadOnlyList<T3Project>>> Projects(CancellationToken cancellationToken) =>
        Answer(() => (Result<IReadOnlyList<T3Project>>)_projects.ToList());

    public ValueTask<Result<T3Model?>> ModelFor(string driverKind, CancellationToken cancellationToken) =>
        Answer(() => (Result<T3Model?>)_models.GetValueOrDefault(driverKind));

    public ValueTask<Result<string>> Launch(T3LaunchRequest request, CancellationToken cancellationToken) => Answer(() =>
    {
        _launched.Add(request);
        var id = $"thread-{_launched.Count}";
        _threads[id] = ("running", false);
        return (Result<string>)id;
    });

    public ValueTask<Result<T3ThreadState?>> Read(string threadId, CancellationToken cancellationToken) =>
        Answer(() => (Result<T3ThreadState?>)(_threads.TryGetValue(threadId, out var t) ? new T3ThreadState(t.Status, t.Archived) : null));

    public ValueTask<Result<Unit>> Interrupt(string threadId, CancellationToken cancellationToken) => Answer(() =>
    {
        _interrupted.Add(threadId);
        if (_threads.TryGetValue(threadId, out var t))
        {
            _threads[threadId] = ("interrupted", t.Archived);
        }

        return (Result<Unit>)Unit.Value;
    });

    private ValueTask<Result<T>> Answer<T>(Func<Result<T>> answer) =>
        ValueTask.FromResult(Down ? SindriErrors.LaunchT3Unavailable.With("the fake T3 is down") : answer());
}
