using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Features;

/// <summary>Registers the context engine's jobs on the P6 job runtime: 'SyncSources', 'BuildContextMap' and 'RunContextBacktest'.</summary>
internal static class ContextJobServiceCollectionExtensions
{
    /// <summary>Adds the jobs, each keyed by a constant: one sync at a time, whatever it names, one build at a time (the profile's one map) and one backtest at a time.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection.</returns>
    internal static IServiceCollection AddContextJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services
            .AddScoped<MapEventSource>()
            .AddScoped<MapJobStarts>()
            .AddSingleton<IIndexOutlooks, NoIndexOutlooks>()   // until CI-9 composes the code index: every repo absent, so the map publishes at once
            .AddJob<SyncContextSourcesRequest, SyncContextSourcesResult, SyncContextSourcesService>(
                static _ => JobKey.From("sync"), static r => SyncSummary(r), PrecheckSync)
            .AddJob<BuildContextMapRequest, BuildContextMapResult, BuildContextMapService>(
                static _ => JobKey.From("build"), static r => string.Create(CultureInfo.InvariantCulture, $"draft v{r.Draft}: {r.Proposed} proposed"))
            .AddJob<RefreshContextMapRequest, RefreshContextMapResult, RefreshContextMapService>(
                static _ => JobKey.From("refresh"), static r => RefreshSummary(r))
            .AddJob<PublishContextMapRequest, PublishContextMapResult, PublishContextMapService>(
                static _ => JobKey.From("map-publish"), static r => PublishSummary(r))
            .AddJob<RunContextBacktestRequest, RunContextBacktestResult, RunContextBacktestService>(
                static _ => JobKey.From("backtest"), static r => BacktestSummary(r), PrecheckBacktest);
    }

    private static string SyncSummary(SyncContextSourcesResult result)
    {
        var failed = result.Sources.Count(static s => s.Links.Code is not null);
        var linked = result.Sources.Sum(static s => s.Links.Linked);
        return string.Create(CultureInfo.InvariantCulture, $"{result.Sources.Count} source(s) synced, {linked} linked{(failed > 0 ? $", {failed} with a code" : string.Empty)}");
    }

    private static string RefreshSummary(RefreshContextMapResult result) => result.Held
        ? string.Create(CultureInfo.InvariantCulture, $"held: map v{result.Version} waits for the code index")
        : string.Create(CultureInfo.InvariantCulture, $"map v{result.Version}: {result.Accepted} kept, {result.Removed} removed{(result.Cleanup == "unavailable" ? ", cleanup unavailable" : string.Empty)}");

    private static string PublishSummary(PublishContextMapResult result) => result switch
    {
        { Code: not null } => string.Create(CultureInfo.InvariantCulture, $"map v{result.Version} was replaced before it could be published ({result.Code})"),
        { Reason: "NotHeld" } => "no map is held",
        { Reason: "Waiting" } => string.Create(CultureInfo.InvariantCulture, $"map v{result.Version} still waits for the code index"),
        { Reason: "Gone" or "AlreadyCurrent" } => string.Create(CultureInfo.InvariantCulture, $"hold on map v{result.Version} dropped ({result.Reason})"),
        _ => string.Create(CultureInfo.InvariantCulture, $"map v{result.Version} promoted · {result.Reason}"),
    };

    private static string BacktestSummary(RunContextBacktestResult result)
    {
        var ran = string.Join('+', result.Arms.Where(static a => a.NotAvailable is null).Select(static a => a.Arm));
        return string.Create(CultureInfo.InvariantCulture, $"{result.Samples} samples, {(ran.Length == 0 ? "no arm" : ran)}, {result.PassLine ?? "no pass line"}");
    }

    /// <summary>An unknown arm is SND-CONTEXT-082, a limit of zero or less or a window that ends before it starts is SND-CLI-003, each before any job exists.</summary>
    private static ValueTask<Result<Unit>> PrecheckBacktest(RunContextBacktestRequest request, CancellationToken cancellationToken)
    {
        var arms = Arms.Parse(request.Arms ?? "all");
        Result<Unit> verdict = arms.IsError ? arms.Error
            : request.Limit is <= 0 ? SindriErrors.InvalidInput.With("limit: must be at least 1 (fix: leave it out for every sample)")
            : request.Since > request.Until ? SindriErrors.InvalidInput.With("since is after until: the window is empty (fix: swap them)")
            : Unit.Value;
        return ValueTask.FromResult(verdict);
    }

    /// <summary>A named source that can't be a source id is unknown (SND-CONTEXT-058) before any job exists.</summary>
    private static ValueTask<Result<Unit>> PrecheckSync(SyncContextSourcesRequest request, CancellationToken cancellationToken)
    {
        Result<Unit> checkedRequest = request.Source is null || SourceId.TryFrom(request.Source, out _)
            ? Unit.Value
            : ContextErrors.SyncSourceUnknown.With($"'{request.Source}' isn't a source id (kebab-case); fix: sindri context sources status");
        return ValueTask.FromResult(checkedRequest);
    }
}
