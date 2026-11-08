using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>
/// The in-memory <see cref="IScopeRunProposals"/>: keeps each run's surfaces (a call replaces them, none withdraws them) and answers every submitted surface as 'new'
/// with a node named from its title, or as 'mapped' when the node already holds it. A call can be made to fail.
/// </summary>
public sealed class InMemoryScopeRunProposals : IScopeRunProposals
{
    private readonly Dictionary<string, List<ProposedScopeSurface>> _runs = new(StringComparer.Ordinal);
    private readonly Error? _error;
    private readonly List<(InstanceAddress Run, RepoName Repo, IReadOnlyList<ProposedScopeSurface> Surfaces)> _calls = [];

    /// <summary>Answers every call.</summary>
    public InMemoryScopeRunProposals()
    {
        _error = null;
    }

    /// <summary>Fails every call with the error.</summary>
    /// <param name="error">The error.</param>
    public InMemoryScopeRunProposals(Error error)
    {
        _error = error;
    }

    /// <summary>The run, repo and surfaces of each call, in order.</summary>
    public IReadOnlyList<(InstanceAddress Run, RepoName Repo, IReadOnlyList<ProposedScopeSurface> Surfaces)> Calls => _calls;

    /// <summary>The surfaces a run holds now.</summary>
    /// <param name="run">The run.</param>
    /// <returns>Its surfaces, none when it never proposed or withdrew.</returns>
    public IReadOnlyList<ProposedScopeSurface> Held(InstanceAddress run) => _runs.TryGetValue(run.Value, out var held) ? held : [];

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<ScopeSurfaceStatus>>> Replace(InstanceAddress run, RepoName repo, IReadOnlyList<ProposedScopeSurface> surfaces, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add((run, repo, surfaces));
        if (_error is { } error)
        {
            return ValueTask.FromResult<Result<IReadOnlyList<ScopeSurfaceStatus>>>(error);
        }

        _runs[run.Value] = [.. surfaces];
        return ValueTask.FromResult(Results.Success<IReadOnlyList<ScopeSurfaceStatus>>([.. surfaces.Select(static s => new ScopeSurfaceStatus(s.Title, "surface:proposed/" + s.Title.ToLowerInvariant().Replace(' ', '-'), "new"))]));
    }
}
