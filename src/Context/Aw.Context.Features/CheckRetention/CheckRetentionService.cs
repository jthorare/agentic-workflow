using System.Globalization;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.CheckRetention;

/// <summary>Doctor 'context:retention' (spec §6.2): warns while expired exports or retained versions past the last 20 are still present.</summary>
internal sealed class CheckRetentionService(IGraphStore store, IExportStore exports, TimeProvider clock, IEvalStore eval) : IService<CheckRetentionRequest, MapHealth>
{
    private const string Name = "context:retention";

    public async ValueTask<Result<MapHealth>> Handle(CheckRetentionRequest request, CancellationToken cancellationToken)
    {
        var files = await exports.List(cancellationToken).ConfigureAwait(false);
        var versions = await store.Versions(cancellationToken).ConfigureAwait(false);
        if (files.IsError || versions.IsError)
        {
            var error = files.IsError ? files.Error : versions.Error;
            return new MapHealth(Name, MapHealthState.Fail, $"{error.Code}: {error.Message}", "check $AW_SINDRI_HOME/context permissions");
        }

        var expired = MapRetention.ExpiredExports(files.Value, clock.GetUtcNow()).Count;
        var excess = MapRetention.ExcessVersions(versions.Value, clock.GetUtcNow()).Count;
        var old = await eval.Expired(clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);   // an unreadable eval folder is not a map problem: counted as none
        var evalFiles = old.IsSuccess ? old.Value.Total : 0;
        if (expired + excess + evalFiles == 0)
        {
            return new MapHealth(Name, MapHealthState.Ok, "no expired exports; retained map versions are within the newest 100 and the 90-day eval window", Fix: null);
        }

        var detail = string.Create(CultureInfo.InvariantCulture,
            $"{expired} export{(expired == 1 ? string.Empty : "s")} older than 14 days and {excess} map version{(excess == 1 ? string.Empty : "s")} past the last 100 still present{(evalFiles == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $", and {evalFiles} eval file{(evalFiles == 1 ? string.Empty : "s")} older than 90 days"))}");
        return new MapHealth(Name, MapHealthState.Warn, detail, "name the context-purge service under services: in the profile; it removes them");
    }
}
