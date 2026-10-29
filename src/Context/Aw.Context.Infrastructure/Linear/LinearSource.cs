using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// The 'linear' source (spec §5.2, D18): Linear's GraphQL API read with hand-written operations, live, without as-of cuts. Search is title and key lookup over the profile's
/// teams (<see cref="LinearSearch"/>); a hit's key is the canonical 'linear:issue/&lt;id&gt;' key, and its alias is the issue identifier.
/// Fetch returns a record while it is at the cited updatedAt (<see cref="LinearRecords"/>); Versions asks once per kind. Probe reads a project, a document and an issue of the profile's teams.
/// </summary>
internal sealed class LinearSource : IContextSource
{
    private static readonly SourceId _id = SourceId.From("linear");
    private readonly LinearSettings _settings;
    private readonly LinearSourceParts _parts;
    private readonly LinearClient _client;
    private readonly LinearScope _scope;

    public LinearSource(LinearSettings settings, LinearSourceParts parts)
    {
        _settings = settings;
        _parts = parts;
        _client = new LinearClient(settings, parts);
        _scope = new LinearScope(_client, new HashSet<string>(settings.Teams, StringComparer.Ordinal));
    }

    /// <inheritdoc />
    public SourceDescriptor Describe() => new(_id, SourceKind.Intent, SourceFreshness.Live, SupportsAsOf: false);

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SourceHit>>> Search(SourceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new LinearSearch(_client, _scope, _parts.Clock).Run(query, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<SourceRecord>> Fetch(SourceRef reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new LinearRecords(_client, _scope).Fetch(reference, _parts.CallDeadline, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<SourceVersion?>>> Versions(IReadOnlyList<SourceRef> refs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refs);
        return new LinearRecords(_client, _scope).Versions(refs, _parts.CallDeadline, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<SourceHealth> Probe(CancellationToken cancellationToken)
    {
        var variables = new System.Text.Json.Nodes.JsonObject();
        if (_scope.IsScoped)
        {
            variables["filter"] = LinearFilters.InTeams([], _scope.Teams);
        }

        var read = await _client.Query<LinearProbeData>("Probe", variables, _parts.CallDeadline, cancellationToken).ConfigureAwait(false);
        return read.IsError
            ? new SourceHealth(SourceState.Unavailable, $"the key check failed: {read.Error.Message}", read.Error.Code)
            : new SourceHealth(SourceState.Ok, $"Linear at {_settings.Address}; key: can read projects, documents and issues of {(_scope.IsScoped ? string.Join(", ", _settings.Teams) : "every team it sees")}", Code: null);
    }
}
