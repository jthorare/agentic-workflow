using System.Text.Json;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Judging;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Core.Visual;

namespace Aw.Evidence.Features.RunScript;

/// <summary>
/// The run's visual verdict through the visual gate: the last passed step's screenshot against the baseline, then the
/// verdict cache (keyed also on whether the judge sends images to Jev, asked once per run), then the judge. No passed step leaves the verdict unchecked. The cache is saved best-effort.
/// </summary>
internal sealed class RunVerdict(VisualGate gate, IJudge judge, IVerdictCacheStore store, IEvidenceSettings settings, JsonSerializerOptions json)
{
    internal const string CacheFile = "verdict-cache.json";

    internal async ValueTask<RunVisual> DecideAsync(VerdictRun run, InvocationLog log, CancellationToken cancellationToken)
    {
        var (request, script, driven) = (run.Request, run.Script, run.Driven);
        var lastPassed = driven.Steps.LastOrDefault(static s => s.Status == StepStatus.Passed);
        if (lastPassed is null)
        {
            return new RunVisual(Verdict.Unchecked, [], DiffScore: null, DiffImage: null);
        }

        var cachePath = request.CacheManifest ?? RunPath.Combine(request.RunDirectory, CacheFile);
        var cache = await store.LoadAsync(cachePath, cancellationToken).ConfigureAwait(false);
        var context = new VerdictCacheContext(
            VerdictCacheContext.CurrentPromptVersion,
            settings.VisualModel,
            request.AppBuild,
            VerdictCacheContext.ScenarioOf(script, json),
            request.Fixtures,
            string.Join(',', script.Viewports.Select(ScriptViewports.NameOf)),
            driven.BrowserVersion,
            await judge.JevImagesOnAsync(cancellationToken).ConfigureAwait(false));
        var input = new VisualGateInput(
            lastPassed.Screenshot,
            request.Baseline,
            driven.Steps.Any(static s => s.Status != StepStatus.Passed),
            run.Instance,
            request.RunDirectory,
            context,
            cache);
        var outcome = await gate.DecideAsync(input, log, cancellationToken).ConfigureAwait(false);
        _ = await store.SaveAsync(cachePath, outcome.Cache, cancellationToken).ConfigureAwait(false);   // a cache is never worth failing a run
        return new RunVisual(outcome.Verdict, outcome.Reasons, outcome.DiffScore, outcome.DiffImage);
    }
}
