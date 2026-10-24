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
using Temporalio.Common;
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
    }

    private bool _fillsInputs;
    private bool _mirrorsInjection;
    private bool _dropsTexts;
    private bool _capsPlan;
    private bool _uncapsPlan;
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
        for (var n = 1; n <= step.Inputs.Count; n++)
        {
            var request = new FillInputRequest(instance.Address.Value, instance.Frame.State, open.Token.Value, n, step.Inputs[n - 1].Use, step.Use, step.Inputs[n - 1].With, instance.Facts, _repo);
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
                var code = e.InnerException is ApplicationFailureException { ErrorType: { } t } ? t : "failed";   // never the message: it could carry text
                return await InputsFailed(instance, open, $"input {request.Input.Value} failed: {code}").ConfigureAwait(true);
            }

            if (_capsPlan && !_uncapsPlan && InjectionPlans.Bytes([.. fills.Select(static f => f.Recorded)]) > InjectionPlans.PlanCap)
            {
                // the plan is one budget for the run: past it the Step fails (SND-WF-019) and the texts held so far go
                return await InputsFailed(instance, open, $"inputs failed: {WorkflowErrors.InjectionInvalid.Code} the plan is over {InjectionPlans.PlanCap} bytes").ConfigureAwait(true);
            }
        }

        _fills = fills;
        _injection = InjectionPlans.Merge([.. fills.Select(static f => f.Recorded)]);
        await MirrorInjection(instance, open).ConfigureAwait(true);
        return default;
    }

    /// <summary>The Step failure an input's failure becomes: its texts dropped, the reason never carrying text.</summary>
    private async Task<Filling> InputsFailed(WorkflowInstance instance, OpenRun open, string reason)
    {
        await DropTexts(instance.Address.Value).ConfigureAwait(true);
        return new Filling(new StepFailed("engine:inputs", open.Token, reason, Tokens: 0, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reason)))), Parked: null);
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
        var detail = WorkflowEventDetail.Of(filled, instance.Id, suspension: null) with { Injection = _injection.Entries };
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
        }

        return fresh;
    }

    private static ActivityOptions FillOptions(OpenRun open, int n, RecipeState state) => new()
    {
        ActivityId = string.Create(CultureInfo.InvariantCulture, $"inputs:{open.Token.Value}:{n}"),
        TaskQueue = StepTaskQueue.For(state, Workflow.Info.TaskQueue),
        StartToCloseTimeout = TimeSpan.FromSeconds(60),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 3, InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2 },
    };

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
