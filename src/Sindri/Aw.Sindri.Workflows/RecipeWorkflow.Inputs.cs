using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Step inputs of <see cref="RecipeWorkflow"/> (plan decisions 1 to 5, 8): before a desk Step opens, each input runs as a
/// 'FillInput' activity ('inputs:{token}:{n}'), in declaration order, once per run token. The workflow merges the metadata
/// the activities return, which is all history holds. An input whose runtime is gone parks the instance with the existing
/// 'no runtime' park (SND-WF-010, the question naming SND-WF-016); any other failure fails the Step.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    // The Step-input markers, read once, first and in this order (before any await): an instance that began before a patch never starts doing its work halfway through.
    private void ReadStepInputMarkers()
    {
        _fillsInputs = Workflow.Patched("step-inputs-v1");
        _mirrorsInjection = Workflow.Patched("step-inputs-injection-row-v1");   // the ledger row adds a local activity: its own marker
        _dropsTexts = Workflow.Patched("step-inputs-drop-v1");   // so does the eviction at the Step's end
        _capsPlan = Workflow.Patched("step-inputs-plan-cap-v1");   // a plan over the cap fails the Step: a history that filled past it before must replay as it ran
        _uncapsPlan = Workflow.Patched("step-inputs-no-plan-cap-v1");   // new runs keep no cap (a big map's view is a long text, not a failure); a history without this marker keeps the cap it ran under
        _unavailable = Workflow.Patched("step-unavailable-v1");   // a Step failure no transition takes is retried, then parks as unavailable; a history without the marker parks needs-answer at once as it did
        _fillsTogether = Workflow.Patched("step-inputs-concurrent-v1");   // a Step's inputs start together, not one after another; a history without the marker ran them one by one
    }

    private bool _fillsInputs;
    private bool _mirrorsInjection;
    private bool _dropsTexts;
    private bool _capsPlan;
    private bool _uncapsPlan;
    private bool _unavailable;
    private bool _fillsTogether;
    private IReadOnlyList<InputTiming> _timings = [];
    private string? _filled;
    private InjectionPlan _injection = InjectionPlan.Empty;
    private List<InjectionFill> _fills = [];
    private bool _textsKept;

    /// <summary>The open run's fills (the pull route's source): metadata and the requests, never text. Empty when the run filled nothing.</summary>
    /// <returns>The state, the run token and the fills.</returns>
    [WorkflowQuery("GetInjection")]
    public InjectionSnapshot GetInjection() => new(_instance?.Frame.State ?? string.Empty, _fills.Count == 0 ? string.Empty : _filled ?? string.Empty, _fills);

    /// <summary>What the inputs did before a run: nothing to report, a Step failure the loop decides, or a park.</summary>
    /// <param name="Failed">The failure trigger; the Step does not open.</param>
    /// <param name="Parked">The parked instance; the Step does not open.</param>
    private readonly record struct Filling(Trigger? Failed, WorkflowInstance? Parked);

    /// <summary>
    /// Runs the state's Step inputs once for this run token. A history recorded before the 'step-inputs-v1' patch fills nothing
    /// (it replays with the commands it had).
    /// </summary>
    private async Task<Filling> FillInputs(WorkflowInstance instance, OpenRun open, RecipeState state)
    {
        var fresh = StartRun(open);
        if (!fresh || !_fillsInputs || state.Step is not { Inputs.Count: > 0 } step)
        {
            return default;
        }

        List<InjectionFill> fills = [];
        _textsKept = true;   // from the first activity on, the process may hold text for this instance
        var stopped = _fillsTogether ? await FillTogether(instance, open, state, step, fills).ConfigureAwait(true) : await FillOneByOne(instance, open, state, step, fills).ConfigureAwait(true);
        if (stopped is { } filling)
        {
            return filling;
        }

        _fills = fills;
        _injection = InjectionPlans.Merge([.. fills.Select(static f => f.Recorded)]);
        _timings = [.. fills.Select(static f => f.Recorded.DurationMs is { } ms ? new InputTiming(f.Recorded.Input, ms) : null).OfType<InputTiming>()];
        await MirrorInjection(instance, open).ConfigureAwait(true);
        return default;
    }

    private FillInputRequest RequestFor(WorkflowInstance instance, OpenRun open, StepUse step, int n) =>
        new(instance.Address.Value, instance.Frame.State, open.Token.Value, n, step.Inputs[n - 1].Use, step.Use, step.Inputs[n - 1].With, instance.Facts, _repo);

    // The inputs one after another, each waiting for the one before: what a history recorded without 'step-inputs-concurrent-v1' ran.
    private async Task<Filling?> FillOneByOne(WorkflowInstance instance, OpenRun open, RecipeState state, StepUse step, List<InjectionFill> fills)
    {
        for (var n = 1; n <= step.Inputs.Count; n++)
        {
            var request = RequestFor(instance, open, step, n);
            try
            {
                fills.Add(new InjectionFill(await Workflow.ExecuteActivityAsync((StepInputActivities a) => a.FillInputAsync(request), FillOptions(open, n, state)).ConfigureAwait(true), request));
            }
            catch (ActivityFailureException e) when (e.InnerException is ApplicationFailureException { ErrorType: var type } && string.Equals(type, WorkflowErrors.StepInputNoRuntime.Code, StringComparison.Ordinal))
            {
                await DropTexts(instance.Address.Value).ConfigureAwait(true);
                return new Filling(Failed: null, await ParkNoRuntime(instance, open, step, request.Input).ConfigureAwait(true));
            }
            catch (ActivityFailureException e) when (e.InnerException is not CanceledFailureException)
            {
                return await InputFailed(instance, open, request.Input, e).ConfigureAwait(true);
            }

            if (OverPlanCap(fills))
            {
                return await PlanOverCap(instance, open).ConfigureAwait(true);
            }
        }

        return null;
    }

    // Every input started in declared order (the same activity ids), then every one awaited, so the texts of a failed run are all in the process
    // before they are dropped; the first failure in declared order decides, as it did one after another.
    private async Task<Filling?> FillTogether(WorkflowInstance instance, OpenRun open, RecipeState state, StepUse step, List<InjectionFill> fills)
    {
        var requests = new List<FillInputRequest>(step.Inputs.Count);
        var running = new List<Task<FilledInput>>(step.Inputs.Count);
        for (var n = 1; n <= step.Inputs.Count; n++)
        {
            var request = RequestFor(instance, open, step, n);
            requests.Add(request);
            running.Add(Workflow.ExecuteActivityAsync((StepInputActivities a) => a.FillInputAsync(request), FillOptions(open, n, state)));
        }

        var failures = new ActivityFailureException?[running.Count];
        for (var i = 0; i < running.Count; i++)
        {
            try
            {
                fills.Add(new InjectionFill(await running[i].ConfigureAwait(true), requests[i]));
            }
            catch (ActivityFailureException e)
            {
                failures[i] = e;
            }
        }

        for (var i = 0; i < failures.Length; i++)
        {
            if (failures[i] is { } failure)
            {
                return await FillFailed(instance, open, step, requests[i].Input, failure).ConfigureAwait(true);
            }
        }

        return OverPlanCap(fills) ? await PlanOverCap(instance, open).ConfigureAwait(true) : null;
    }

    // One fill's failure, as the one-after-another path decides it: no runtime parks, a cancelled fill rethrows, anything else fails the Step.
    private async Task<Filling> FillFailed(WorkflowInstance instance, OpenRun open, StepUse step, BlockId input, ActivityFailureException failure)
    {
        if (failure.InnerException is ApplicationFailureException { ErrorType: var type } && string.Equals(type, WorkflowErrors.StepInputNoRuntime.Code, StringComparison.Ordinal))
        {
            await DropTexts(instance.Address.Value).ConfigureAwait(true);
            return new Filling(Failed: null, await ParkNoRuntime(instance, open, step, input).ConfigureAwait(true));
        }

        if (failure.InnerException is CanceledFailureException)
        {
            throw failure;   // a cancelled fill: the workflow is being cancelled
        }

        return await InputFailed(instance, open, input, failure).ConfigureAwait(true);
    }

    private bool OverPlanCap(List<InjectionFill> fills) => _capsPlan && !_uncapsPlan && InjectionPlans.Bytes([.. fills.Select(static f => f.Recorded)]) > InjectionPlans.PlanCap;

    // the plan is one budget for the run: past it the Step fails (SND-WF-019) and the texts held so far go
    private async Task<Filling?> PlanOverCap(WorkflowInstance instance, OpenRun open) =>
        await InputsFailed(instance, open, $"inputs failed: {WorkflowErrors.InjectionInvalid.Code} the plan is over {InjectionPlans.PlanCap} bytes").ConfigureAwait(true);

    // The reason is a code, never the message (it could carry text); a typed SND- code also becomes the park's.
    private Task<Filling> InputFailed(WorkflowInstance instance, OpenRun open, BlockId input, ActivityFailureException failure)
    {
        var code = failure.InnerException is ApplicationFailureException { ErrorType: { } type } && type.StartsWith("SND-", StringComparison.Ordinal) ? type : null;
        return InputsFailed(instance, open, InputFillPolicy.Reason(input.Value, failure.InnerException), input.Value, code);
    }

    /// <summary>
    /// The Step failure an input's failure becomes: its texts dropped, the reason never carrying text. The input is the dependency that was down, so a failure no
    /// transition takes is retried and then parks as unavailable, naming it (owner decision q93).
    /// </summary>
    private async Task<Filling> InputsFailed(WorkflowInstance instance, OpenRun open, string reason, string? dependency = null, string? code = null)
    {
        await DropTexts(instance.Address.Value).ConfigureAwait(true);
        return new Filling(new StepFailed("engine:inputs", open.Token, reason, Tokens: 0, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reason)))) { Dependency = dependency, Code = code }, Parked: null);
    }

    /// <summary>
    /// Mirrors the run's injection plan (metadata only) once per run that has inputs: a 'workflow.injection' row. It is not a
    /// transition, so it neither counts as a version nor changes the item's or the needs-you views.
    /// </summary>
    private async Task MirrorInjection(WorkflowInstance instance, OpenRun open)
    {
        if (!_mirrorsInjection || _injection.Entries.Count == 0)
        {
            return;
        }

        var filled = new StepCompleted("engine:inputs", open.Token, OutcomeName.From("filled"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, string.Empty);
        var detail = WorkflowEventDetail.Of(filled, instance.Id, suspension: null) with { Injection = _injection.Entries, Inputs = _timings.Count == 0 ? null : _timings };
        // why: a Step runs only after RunAsync set _start
        var start = _start!;
        var record = new WorkflowEventRecord(start.WorkflowId().Value, EventTypes.WorkflowInjection, start.Item.Value, instance.Frame.State, instance.Frame.State, "inputs", Outcome: null, open.Token.Value, "engine:inputs", start.Tenant, start.Ring, detail);
        _ = await Workflow.ExecuteLocalActivityAsync((RecordEventActivities a) => a.RecordAsync(record), RecordEventActivities.LocalOptions).ConfigureAwait(true);
    }

    /// <summary>
    /// Drops the process's kept text for this instance once its Step has ended (engine condition 2: a cancelled or failed Step leaks
    /// no entries). A local activity, so workflow code stays free of side effects, detached from the workflow's cancellation so a
    /// cancelled instance still cleans up. Does nothing when no input ran, so a history without fills gets no new command.
    /// </summary>
    /// <param name="address">The instance address.</param>
    private async Task DropTexts(string address)
    {
        if (!_dropsTexts || !_textsKept)
        {
            return;
        }

        _textsKept = false;
        await Workflow.ExecuteLocalActivityAsync(
            (StepInputActivities a) => a.DropInjectionAsync(address),
            new LocalActivityOptions { StartToCloseTimeout = TimeSpan.FromSeconds(10), CancellationToken = CancellationToken.None }).ConfigureAwait(true);
    }

    /// <summary>The run-close path for every runtime (desk, block, agent): once the instance ended, was suspended, or moved to another run, the process's texts for it go.</summary>
    private async Task DropEndedRun(WorkflowInstance instance)
    {
        if (_textsKept && (instance.Ended || instance.Suspension is not null || !string.Equals(instance.Frame.Run?.Token.Value, _filled, StringComparison.Ordinal)))
        {
            await DropTexts(instance.Address.Value).ConfigureAwait(true);
        }
    }

    /// <summary>Notes the run token the plan belongs to; a new token clears the previous run's plan (the plan shown is the open run's).</summary>
    private bool StartRun(OpenRun open)
    {
        var fresh = !string.Equals(_filled, open.Token.Value, StringComparison.Ordinal);
        _filled = open.Token.Value;
        if (fresh)
        {
            _injection = InjectionPlan.Empty;
            _fills = [];
            _timings = [];
        }

        return fresh;
    }

    private static ActivityOptions FillOptions(OpenRun open, int n, RecipeState state) =>
        InputFillPolicy.Options(string.Create(CultureInfo.InvariantCulture, $"inputs:{open.Token.Value}:{n}"), StepTaskQueue.For(state, Workflow.Info.TaskQueue));

    /// <summary>Parks the instance for the input whose runtime is gone, mirrors it, and stops the Step from opening.</summary>
    private async Task<WorkflowInstance> ParkNoRuntime(WorkflowInstance before, OpenRun open, StepUse step, BlockId input)
    {
        var parked = Enter(InputParks.Park(before, input, step.Use, "engine:inputs"), "engine:inputs");
        var trigger = new StepFailed("engine:inputs", open.Token, InputParks.Question(input, step.Use), Tokens: 0, ReportSha256: string.Empty);   // why: the row's trigger; the park is not decided through it
        // why: a Step runs only after RunAsync set _start
        await Mirror(Transition(_start!, before, parked, trigger, open), NeedsYou(parked)).ConfigureAwait(true);
        return parked;
    }
}
