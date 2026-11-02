using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.BuildMap;

/// <summary>
/// 'context map checks' (map structure spec §4.3): the two structure measures of the current map, the draft, or the map a build would save. A human's read: it lists feature keys, so a session is refused.
/// The first two read the stored graph; <see cref="MapChecksSource.Build"/> runs the build's reading in memory (no graphify, no model call, nothing written) under one heavy-job slot, which is
/// the way to see the numbers of a refused build, since a refusal saves no draft.
/// </summary>
internal sealed class CheckStructureService(IGraphStore store, BuildMapService build, IHeavyJobLock heavy) : IService<CheckStructureRequest, MapChecksView>
{
    public async ValueTask<Result<MapChecksView>> Handle(CheckStructureRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        if (scope.Caller.Kind == ContextCallerKind.Session)
        {
            return ContextErrors.SessionsDisabled.With("'context map checks' lists feature keys, so it is a human's command (run it from your own terminal)");
        }

        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var (mode, limits) = (settings.Value.Checks, settings.Value.CheckLimits);
        return request.Source == MapChecksSource.Build
            ? await Build(request, mode, cancellationToken).ConfigureAwait(false)
            : await Stored(request.Source == MapChecksSource.Draft, (mode, limits), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<MapChecksView>> Stored(bool draft, (ChecksMode Mode, MapCheckLimits Limits) checks, CancellationToken cancellationToken)
    {
        var snapshot = await store.Snapshot(draft, cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError)
        {
            return snapshot.Error;
        }

        return snapshot.Value is { } found
            ? new MapChecksView(draft ? MapChecksSource.Draft : MapChecksSource.Current, found.Version.Number, checks.Mode, MapChecks.Measure(found.Graph, checks.Limits))
            : draft
                ? ContextErrors.NoDraftMap.With("there is no draft map (fix: sindri context map build, or measure what a build would save: sindri context map checks --build)")
                : ContextErrors.NoMap.With("there is no map yet (fix: sindri context map import <file>, or measure what a build would save: sindri context map checks --build)");
    }

    private async ValueTask<Result<MapChecksView>> Build(CheckStructureRequest request, ChecksMode mode, CancellationToken cancellationToken)
    {
        var slot = await heavy.Take(request.Progress, cancellationToken).ConfigureAwait(false);
        if (slot.IsError)
        {
            return slot.Error;
        }

        await using (slot.Value.ConfigureAwait(false))
        {
            var measured = await build.Measure(new BuildMapRequest(request.Progress, request.Scope), cancellationToken).ConfigureAwait(false);
            return measured.IsError ? measured.Error : new MapChecksView(MapChecksSource.Build, Version: null, mode, measured.Value);
        }
    }
}
