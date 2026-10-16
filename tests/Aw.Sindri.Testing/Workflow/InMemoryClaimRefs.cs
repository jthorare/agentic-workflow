using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IClaimRefs"/>: the refs marked, by repo and name. Passes <see cref="ClaimRefsContract"/>.</summary>
public sealed class InMemoryClaimRefs : IClaimRefs
{
    private readonly HashSet<(string Repository, string Reference)> _marked = [];
    private readonly Lock _gate = new();

    /// <summary>Whether <paramref name="reference"/> was marked in <paramref name="repository"/>.</summary>
    /// <param name="repository">The repo path.</param>
    /// <param name="reference">The full ref name.</param>
    /// <returns>True when marked.</returns>
    public bool Has(string repository, string reference)
    {
        lock (_gate)
        {
            return _marked.Contains((repository, reference));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Mark(string repository, string reference, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return ValueTask.FromResult<Result<bool>>(_marked.Add((repository, reference)));
        }
    }
}
