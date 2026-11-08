using Aw.Context.Core.Graph;
using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// <see cref="IScopeRunProposals"/> over the proposal command's own service, run in process without the command chain (the chain belongs to a caller's
/// request: the Step is the engine acting as the service), so the draft rows and the 'context.map.proposed' event are what 'context map propose' writes.
/// </summary>
/// <param name="service">The proposal command's service.</param>
internal sealed class ScopeRunProposals(ProposeContextMapService service) : IScopeRunProposals
{
    private static readonly HashSet<string> _kinds = new(["feature", "surface", "action", "thing", "flow"], StringComparer.Ordinal);

    /// <inheritdoc/>
    // The engine answers in submission order and fences a surface name it echoes back for a service caller, so each status is named by the title that was submitted.
    public async ValueTask<Result<IReadOnlyList<ScopeSurfaceStatus>>> Replace(InstanceAddress run, RepoName repo, IReadOnlyList<ProposedScopeSurface> surfaces, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(surfaces);
        var proposed = surfaces.Select(static s => new ProposedSurface(s.Title, Key: null, s.Kind is { } kind && _kinds.Contains(kind) && Enum.TryParse<NodeKind>(kind, ignoreCase: true, out var parsed) ? parsed : null)).ToList();
        var written = await service.Handle(new ProposeContextMapRequest(run.Value, repo.Value, proposed), cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : Results.Success<IReadOnlyList<ScopeSurfaceStatus>>([.. written.Value.Surfaces.Select((s, i) => new ScopeSurfaceStatus(i < surfaces.Count ? surfaces[i].Title : s.Surface, s.Node?.Value, Status(s.Status)))]);
    }

    private static string Status(GraphStatusKind kind) => kind switch
    {
        GraphStatusKind.Mapped => "mapped",
        GraphStatusKind.New => "new",
        GraphStatusKind.Unmapped => "unmapped",
        _ => "unmapped",
    };
}
