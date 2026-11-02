using Aw.Kernel;
using Aw.Sindri.Core.Projects.Facts;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="IProjectFacts"/>: a fixed snapshot and freshness, or an error for either; records the attached keys each read asked for.</summary>
public sealed class FakeProjectFacts : IProjectFacts
{
    private Result<ProjectFactsSnapshot> _snapshot = new ProjectFactsSnapshot(Owner: null, [], [], [], FirstReadDone: true);
    private Result<IReadOnlyList<ProjectSourceFreshness>> _freshness = Results.Success<IReadOnlyList<ProjectSourceFreshness>>([]);

    private readonly List<IReadOnlyList<string>> _asked = [];

    /// <summary>The attached keys each read asked for, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Asked => _asked;

    /// <summary>Answers every read with <paramref name="snapshot"/>.</summary>
    /// <param name="snapshot">The facts.</param>
    /// <returns>The same instance.</returns>
    public FakeProjectFacts With(ProjectFactsSnapshot snapshot)
    {
        _snapshot = snapshot;
        return this;
    }

    /// <summary>Fails every read with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeProjectFacts Failing(Error error)
    {
        _snapshot = error;
        _freshness = error;
        return this;
    }

    /// <summary>Answers the freshness read with <paramref name="rows"/>.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The same instance.</returns>
    public FakeProjectFacts WithFreshness(params ProjectSourceFreshness[] rows)
    {
        _freshness = Results.Success<IReadOnlyList<ProjectSourceFreshness>>(rows);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<ProjectFactsSnapshot>> Read(IReadOnlyList<string> attached, CancellationToken cancellationToken)
    {
        _asked.Add(attached);
        return ValueTask.FromResult(_snapshot);
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ProjectSourceFreshness>>> Freshness(CancellationToken cancellationToken) => ValueTask.FromResult(_freshness);
}
