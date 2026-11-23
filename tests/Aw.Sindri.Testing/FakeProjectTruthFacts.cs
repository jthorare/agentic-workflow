using Aw.Kernel;
using Aw.Sindri.Core.Projects.TruthSources;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="IProjectTruthFacts"/>: fixed facts (none by default) or an error; records the sources each read asked for.</summary>
public sealed class FakeProjectTruthFacts : IProjectTruthFacts
{
    private readonly List<ProjectSources> _asked = [];
    private Result<ProjectTruthFacts> _facts = new ProjectTruthFacts([], new Dictionary<string, ReviewSummary>(StringComparer.Ordinal), [], []);

    /// <summary>The union each read asked for, in order.</summary>
    public IReadOnlyList<ProjectSources> Asked => _asked;

    /// <summary>Answers every read with <paramref name="facts"/>.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The same instance.</returns>
    public FakeProjectTruthFacts With(ProjectTruthFacts facts)
    {
        _facts = facts;
        return this;
    }

    /// <summary>Fails every read with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeProjectTruthFacts Failing(Error error)
    {
        _facts = error;
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<ProjectTruthFacts>> Read(ProjectSources union, CancellationToken cancellationToken)
    {
        _asked.Add(union);
        return ValueTask.FromResult(_facts);
    }
}
