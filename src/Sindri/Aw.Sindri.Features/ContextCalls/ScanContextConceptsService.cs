using System.Globalization;
using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a scan job (owner, q110): finds the newest map version (the latest draft, else the current one), refuses over the daily budget of <see cref="ConceptScanLimits.MaxScansPerDay"/> (counted from the jobs
/// view over the last 24 hours) and otherwise asks the engine to scan it. It takes no heavy-job slot. A scan that stopped over budget, found no map, got no answer from a model or found no concept ends succeeded with that
/// outcome, so a bad day for the model is never a failed job and never a failed map build; only an engine error (the store, the profile) fails it.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The 'jobs' view: the scans of the last day.</param>
/// <param name="clock">Dates the budget window.</param>
internal sealed class ScanContextConceptsService(ContextScopes scopes, IContextEngine engine, IJobViews jobs, TimeProvider clock) : IService<ScanContextConceptsRequest, ScanContextConceptsResult>
{
    public async ValueTask<Result<ScanContextConceptsResult>> Handle(ScanContextConceptsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var state = await engine.Held(scope.Value, cancellationToken).ConfigureAwait(false);
        if (state.IsError)
        {
            return state.Error;
        }

        var target = state.Value.LatestDraft ?? state.Value.Current;
        if (target is not { } version)
        {
            return new ScanContextConceptsResult(Version: 0, OnDraft: false, "no-map", Hints: 0, Searches: 0, "no map to scan");
        }

        var onDraft = state.Value.LatestDraft is not null;
        var refusal = await Refusal(cancellationToken).ConfigureAwait(false);
        if (refusal.IsError)
        {
            return refusal.Error;
        }

        if (refusal.Value is { } reason)
        {
            return new ScanContextConceptsResult(version, onDraft, ConceptScanLog.OverBudget, Hints: 0, Searches: 0, reason);
        }

        var report = await engine.ScanConcepts(scope.Value, cancellationToken).ConfigureAwait(false);   // whatever is newest now: a refresh may have promoted the draft since the state was read
        return report.IsError ? report.Error : Of(report.Value);
    }

    private async ValueTask<Result<string?>> Refusal(CancellationToken cancellationToken)
    {
        var scans = await jobs.List(state: null, ContextConceptScanService.Kind, cancellationToken).ConfigureAwait(false);
        if (scans.IsError)
        {
            return scans.Error;
        }

        return Results.Success(ConceptScanBudget.Refusal(ConceptScanLog.Started(scans.Value, clock.GetUtcNow() - ConceptScanLimits.Window)));
    }

    private static ScanContextConceptsResult Of(ConceptScanReport report) => new(report.Version, report.OnDraft, Outcome(report.Outcome), report.Hints, report.Searches, Reason: null);

    private static string Outcome(ConceptScanOutcome outcome) => outcome switch
    {
        ConceptScanOutcome.Scanned => "scanned",
        ConceptScanOutcome.NothingToScan => "nothing-to-scan",
        ConceptScanOutcome.ModelUnavailable => "model-unavailable",
        ConceptScanOutcome.NoConcepts => "no-concepts",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "an unknown scan outcome"),
    };

    /// <summary>The job summary: what the scan did in one line.</summary>
    internal static string Summary(ScanContextConceptsResult result) => result.Outcome switch
    {
        "scanned" => string.Create(CultureInfo.InvariantCulture, $"map v{result.Version}: {result.Hints} concept hint(s) from {result.Searches} search(es)"),
        "no-map" => "no map to scan",
        ConceptScanLog.OverBudget => string.Create(CultureInfo.InvariantCulture, $"map v{result.Version}: {result.Reason}"),
        _ => string.Create(CultureInfo.InvariantCulture, $"map v{result.Version}: {result.Outcome.Replace('-', ' ')}"),
    };
}
