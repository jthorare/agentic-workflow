using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Host.Models;

/// <summary>
/// The context engine's model port with routing (model routing spec §4) over Judge's provider catalog. It asks only the owner's own agent CLIs (Claude Code, Codex, Cursor), never the remote 'jev' API, because the prompt carries the
/// owner's repo text. For each call it finds the kind from the purpose, reads 'context.models' of the approved profile, lets <see cref="ModelRouting"/> pick the harnesses and the model each runs, and walks them in order
/// (<see cref="ModelWalker"/>). When nothing answered and a harness is at its limit, a call that runs under a job parks the job on 'model-limit' (<see cref="ModelPauser"/>) and walks again from the first harness when it is resumed; every
/// other end is SND-CONTEXT-240, 241 or 042 (<see cref="ModelCallErrors"/>). The tries become 'context.model.called' events (<see cref="ModelCallRecorder"/>). A request's own model names win over the profile and go through the same name check.
/// </summary>
internal sealed class RoutedModelPort : IModelPort
{
    private static readonly ProviderName[] _agentClis = [.. AgentCliNames.DefaultOrder.Select(AgentCliNames.Provider)];

    private readonly Func<IProviderCatalog> _catalog;
    private readonly IApprovedModels _models;
    private readonly IJobContextAccessor _jobs;
    private readonly JobModelLimits _limits = new();
    private readonly ModelWalker _walker;
    private readonly ModelPauser _pauser;
    private readonly ModelCallRecorder _recorder;

    /// <summary>Creates the port.</summary>
    /// <param name="catalog">Finds the provider lineup (resolved on the first request).</param>
    /// <param name="models">'context.models' of the approved profile.</param>
    /// <param name="jobs">The job the call runs under, and the handle that parks it.</param>
    /// <param name="log">Where the call's event is written.</param>
    /// <param name="ids">Names each call's event.</param>
    /// <param name="clock">Dates and times the tries.</param>
    public RoutedModelPort(Func<IProviderCatalog> catalog, IApprovedModels models, IJobContextAccessor jobs, IModelCallLog log, IIdGenerator ids, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(jobs);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(clock);
        _catalog = catalog;
        _models = models;
        _jobs = jobs;
        _recorder = new ModelCallRecorder(log, ids, clock);
        _walker = new ModelWalker(_limits, clock);
        _pauser = new ModelPauser(_limits, _recorder);
    }

    public async ValueTask<Result<ModelAnswer>> Ask(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (BadName(request) is { } refused)
        {
            return refused;
        }

        _limits.Prune();
        var call = await Begin(request, cancellationToken).ConfigureAwait(false);
        if (call.Route.Count == 0)
        {
            return await Fail(call, ModelCallErrors.NoHarness(call)).ConfigureAwait(false);
        }

        while (true)
        {
            if (await _walker.Walk(call, cancellationToken).ConfigureAwait(false) is { } answer)
            {
                await _recorder.Write(call, answer).ConfigureAwait(false);
                return answer;
            }

            if (!call.HitLimit)
            {
                return await Fail(call, ModelCallErrors.NoAnswer()).ConfigureAwait(false);
            }

            if (call.Job is not { Waiting: { } waiting } job || !await _pauser.Park(call, job, waiting, cancellationToken).ConfigureAwait(false))
            {
                return await Fail(call, ModelCallErrors.Unavailable(call)).ConfigureAwait(false);   // no job to park, or the park ended by cancel or the 24 hours
            }
        }
    }

    private async ValueTask<ModelCall> Begin(ModelRequest request, CancellationToken cancellationToken)
    {
        var settings = await _models.Current(cancellationToken).ConfigureAwait(false);
        var installed = Installed(_catalog().Lineup(config: null, _agentClis, new NoRulesProvider()));
        var call = new ModelCall(request, settings, _jobs.Current, installed) { Route = ModelRouting.Resolve(settings, request, [.. installed.Select(static i => i.Harness)]) };
        _recorder.Begin(call);
        return call;
    }

    private async ValueTask<Result<ModelAnswer>> Fail(ModelCall call, Error error)
    {
        await _recorder.Write(call, answer: null).ConfigureAwait(false);
        return error;
    }

    // Each installed harness with its provider, in the chain's order (the AW_PROVIDER one first); a harness not on PATH is not in the lineup, so it is never a try.
    private static List<InstalledHarness> Installed(ProviderLineup lineup)
    {
        var found = new List<InstalledHarness>();
        foreach (var name in lineup.Chain.For(ContentClass.Code))
        {
            if (HarnessOf(name) is { } harness && lineup.Providers.FirstOrDefault(p => p.Name == name) is { } provider)
            {
                found.Add(new InstalledHarness(harness, provider));
            }
        }

        return found;
    }

    private static ModelHarness? HarnessOf(ProviderName name) => name switch
    {
        ProviderName.ClaudeCli => ModelHarness.ClaudeCode,
        ProviderName.CodexCli => ModelHarness.Codex,
        ProviderName.CursorCli => ModelHarness.Cursor,
        ProviderName.Rules or ProviderName.Jev => null,
        _ => null,
    };

    // A name the request carries must pass the profile's name check before it reaches a CLI's --model (spec 3.2 point 5).
    private static Error? BadName(ModelRequest request) =>
        (request.Models?.Values ?? []).Concat(request.Model is null ? [] : [request.Model]).FirstOrDefault(static n => !ModelNames.IsValid(n)) is { } bad
            ? ContextErrors.SettingsInvalid.With($"the request's model name '{bad}' is not a model name (letters, digits and . _ : / @ + -, not starting with -)")
            : null;
}
