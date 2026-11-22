using System.Globalization;
using Aw.Context.Core.Concepts;
using Aw.Context.Core.Map;
using Aw.Context.Testing.Linking;
using Aw.Context.Testing.Map;
using Aw.Kernel;

namespace Aw.Context.Testing.Concepts;

/// <summary>
/// The in-memory concept answer store over a <see cref="FakeGraphStore"/> and a <see cref="FakeIntentStore"/>; <see cref="ConceptAnswerStoreContract"/> holds it to the SQLite
/// store's behavior. Its basis reads the versions and overrides of the graph fake, never a snapshot, so a test that counts snapshot reads counts only the map loads.
/// </summary>
/// <param name="graphs">The graph store whose versions and overrides the basis reads.</param>
/// <param name="intents">The intent store whose counter the basis reads.</param>
public sealed class FakeConceptAnswerStore(FakeGraphStore graphs, FakeIntentStore intents) : IConceptAnswerStore
{
    private readonly Dictionary<string, (int Version, object View)> _rows = new(StringComparer.Ordinal);

    /// <summary>How many answers are kept.</summary>
    public int Count => _rows.Count;

    /// <inheritdoc/>
    public async ValueTask<Result<ConceptAnswerBasis?>> Basis(MapRead read, CancellationToken cancellationToken)
    {
        var versions = await graphs.Versions(cancellationToken).ConfigureAwait(false);
        var overrides = versions.IsError ? versions.Error : await graphs.Overrides(cancellationToken).ConfigureAwait(false);
        if (overrides.IsError)
        {
            return overrides.Error;
        }

        var found = read.Version is { } asked ? versions.Value.FirstOrDefault(v => v.Number == asked.Number && v.Status is MapVersionStatus.Current or MapVersionStatus.Retained)
            : versions.Value.Where(v => v.Status == (read.Draft ? MapVersionStatus.Draft : MapVersionStatus.Current)).MaxBy(v => v.Number);
        var count = overrides.Value.Count.ToString(CultureInfo.InvariantCulture);
        return found is null ? Results.Success<ConceptAnswerBasis?>(value: null) : new ConceptAnswerBasis(found.Number, count + ":" + count, intents.Stamp);
    }

    /// <inheritdoc/>
    public ValueTask<Result<ConceptsView?>> Concepts(string key, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(_rows.TryGetValue(key, out var row) ? row.View as ConceptsView : null));

    /// <inheritdoc/>
    public ValueTask<Result<ConceptGlossaryView?>> Glossary(string key, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Results.Success(_rows.TryGetValue(key, out var row) ? row.View as ConceptGlossaryView : null));

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptsView view, CancellationToken cancellationToken) => Put(key, basis, view, cancellationToken);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Keep(string key, ConceptAnswerBasis basis, ConceptGlossaryView view, CancellationToken cancellationToken) => Put(key, basis, view, cancellationToken);

    // As the SQLite store: the row, then the rows of versions the graph store no longer holds are dropped.
    private async ValueTask<Result<Unit>> Put(string key, ConceptAnswerBasis basis, object view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(basis);
        var versions = await graphs.Versions(cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        _rows[key] = (basis.Version, view);
        var held = versions.Value.Select(v => v.Number).ToHashSet();
        foreach (var gone in _rows.Where(r => !held.Contains(r.Value.Version)).Select(r => r.Key).ToList())
        {
            _ = _rows.Remove(gone);
        }

        return Unit.Value;
    }
}
