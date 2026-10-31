using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ExportMap;

/// <summary>
/// Writes the current version with its override layer to the exports folder (spec Phase 3): nodes, edges with their
/// evidence, aliases, decisions and overrides with their original timestamps and provenance; never trust. A session
/// can't export (SND-CONTEXT-020, spec §3): the file holds unscrubbed work text.
/// </summary>
internal sealed class ExportMapService(IGraphStore store, IExportStore exports, TimeProvider clock) : IService<ExportMapRequest, MapExported>
{
    public async ValueTask<Result<MapExported>> Handle(ExportMapRequest request, CancellationToken cancellationToken)
    {
        if (Refusal(request.Scope) is { } refused)
        {
            return refused;
        }

        var current = await store.Current(cancellationToken).ConfigureAwait(false);
        if (current.IsError || current.Value is not { } version)
        {
            return current.IsError ? current.Error : ContextErrors.NoMap.With("there is no current map to export (fix: sindri context map import <file>)");
        }

        var graph = await store.Read(version.Number, cancellationToken).ConfigureAwait(false);
        var overrides = await store.Overrides(cancellationToken).ConfigureAwait(false);
        if (graph.IsError || overrides.IsError)
        {
            return graph.IsError ? graph.Error : overrides.Error;
        }

        var visible = MapAccess.View(graph.Value, overrides.Value, request.Scope with { OmitPages = true }, pending: false);
        if (visible.IsError)
        {
            return visible.Error;
        }

        var now = clock.GetUtcNow();
        var document = MapAccess.Export(version, visible.Value.Graph, visible.Value.Overrides, now);
        var name = string.Create(CultureInfo.InvariantCulture, $"map-v{version.Number}-{now.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}.json");
        var written = await exports.Write(request.Path, name, document, cancellationToken).ConfigureAwait(false);
        return written.IsError
            ? written.Error
            : new MapExported(written.Value, version.Number, MapCounts.Of(visible.Value.Graph, visible.Value.Overrides.Count));
    }

    private static Error? Refusal(ContextScope scope) => scope.Caller.Kind == ContextCallerKind.Session
        ? ContextErrors.SessionsDisabled.With("exporting the map is a human or service job: a session can't (it writes unscrubbed work text)")
        : MapAccess.Gate(scope);
}
