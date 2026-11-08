using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Shows or performs a map import (context spec §8.7): without a hash, the checked file and its content hash; with a hash,
/// a file that changed since is SND-CONTEXT-034, otherwise the engine imports and 'context.map.imported' is appended. No typed
/// challenge is asked for (owner decision q93).
/// </summary>
internal sealed class ImportContextMapService(ContextScopes scopes, IContextEngine engine, HeldLease held, ILedgerStore ledger, EventFactory events)
    : IService<ImportContextMapRequest, ImportContextMapResponse>
{
    public async ValueTask<Result<ImportContextMapResponse>> Handle(ImportContextMapRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var preview = await engine.PreviewMap(request.Path, scope.Value, cancellationToken).ConfigureAwait(false);
        if (preview.IsError || request.Hash is not { } hash)
        {
            return preview.IsError ? preview.Error : Previewed(preview.Value);
        }

        if (Refusal(hash, preview.Value.Hash) is { } refused)
        {
            return refused;
        }

        if (held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var imported = await engine.ImportMap(request.Path, hash, scope.Value, cancellationToken).ConfigureAwait(false);
        if (imported.IsError)
        {
            return imported.Error;
        }

        var v = imported.Value;
        var payload = new ContextMapImported(v.Version.Number, v.Version.Repo.Value, v.Hash, v.Counts.Nodes, v.Counts.Edges, v.Counts.Overrides);
        var key = string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ContextMapImported}:{v.Hash}:v{v.Version.Number}");
        var appended = await ledger.Append(events.Entry(EventTypes.ContextMapImported, payload, key), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : new ImportContextMapResponse(preview.Value, v, appended.Value.Sequence);
    }

    private static ImportContextMapResponse Previewed(MapFilePreview preview) => new(preview, Imported: null, Sequence: null);

    /// <summary>A hash that isn't the file's now (034).</summary>
    private static Error? Refusal(string asked, string live) => !string.Equals(asked, live, StringComparison.Ordinal)
        ? ContextErrors.MapFileChanged.With(
            $"the file's content hash is now {live}, not the one you confirmed; nothing was written (fix: run 'sindri context map import <file>' again)")
        : null;
}
