using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Features.LinearIssues;

namespace Aw.Sindri.Testing;

/// <summary>The in-memory <see cref="ILinearItemLinks"/>: the item and pull request of chosen records, or an error.</summary>
[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "Internal because ILinearItemLinks is; Aw.Sindri.Tests uses it through InternalsVisibleTo, which MA0182 can't see.")]
internal sealed class FakeLinearItemLinks : ILinearItemLinks
{
    private readonly Dictionary<string, LinearIssueLinks> _links = new(StringComparer.Ordinal);
    private Error? _failing;

    /// <summary>Links <paramref name="record"/> to an item and, when given, its pull request.</summary>
    /// <param name="record">The issue's record key.</param>
    /// <param name="item">The item.</param>
    /// <param name="pr">The pull request, or null.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearItemLinks With(string record, LinearItemLinkView item, LinearPrView? pr = null)
    {
        _links[record] = new LinearIssueLinks(item, pr);
        return this;
    }

    /// <summary>Fails every read with <paramref name="error"/>.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The same instance.</returns>
    public FakeLinearItemLinks Failing(Error error)
    {
        _failing = error;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyDictionary<string, LinearIssueLinks>>> For(IReadOnlyList<string> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);
        return _failing is { } error
            ? ValueTask.FromResult<Result<IReadOnlyDictionary<string, LinearIssueLinks>>>(error)
            : ValueTask.FromResult(Results.Success<IReadOnlyDictionary<string, LinearIssueLinks>>(_links.Where(l => records.Contains(l.Key, StringComparer.Ordinal)).ToDictionary(static l => l.Key, static l => l.Value, StringComparer.Ordinal)));
    }
}
