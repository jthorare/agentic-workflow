using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ImportMap;

/// <summary>
/// Imports a map file as the current version (spec Phase 3, §8.7). It is bound to the content hash the owner confirmed:
/// a file that changed since is SND-CONTEXT-034, and nothing is written on any refusal. Sindri's protected-command
/// gate (a human at a terminal) runs before this.
/// </summary>
internal sealed class ImportMapService(IMapFileReader reader, IGraphStore store, TimeProvider clock) : IService<ImportMapRequest, MapImported>
{
    public async ValueTask<Result<MapImported>> Handle(ImportMapRequest request, CancellationToken cancellationToken)
    {
        if (request.Scope.Caller.Kind == ContextCallerKind.Session)
        {
            return ContextErrors.SessionsDisabled.With("importing a map is a human decision: a session can't preview or import one");
        }

        var file = await reader.Read(request.Path, cancellationToken).ConfigureAwait(false);
        if (file.IsError)
        {
            return file.Error;
        }

        var map = MapDocumentValidator.Check(file.Value.Document, KeyMigrations.Shipped);
        if (map.IsError)
        {
            return map.Error;
        }

        if (MapAccess.Maps(map.Value.Repo, request.Scope) is { } unmapped)
        {
            return unmapped;
        }

        if (!string.Equals(file.Value.Hash, request.ConfirmedHash, StringComparison.OrdinalIgnoreCase))
        {
            return ContextErrors.MapFileChanged.With(
                $"{request.Path} now hashes to {file.Value.Hash[..12]}…, not the confirmed {request.ConfirmedHash[..Math.Min(12, request.ConfirmedHash.Length)]}…; nothing was written (fix: run 'context map import' again and confirm the new hash)");
        }

        var saved = await store.SaveCurrent(map.Value with { Ruleset = MapRuleset.Running(request.Scope) }, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return saved.IsError ? saved.Error : new MapImported(saved.Value, map.Value.Counts, file.Value.Hash);
    }
}
