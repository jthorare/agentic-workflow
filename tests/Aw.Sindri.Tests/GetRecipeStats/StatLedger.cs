using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.GetRecipeStats;

/// <summary>A fixture ledger of workflow rows for the recipe stats routes: each row is appended at the instant the host's fake clock is set to.</summary>
internal sealed class StatLedger(GoldenHost host, string recipe, string hash)
{
    private int _next;

    /// <summary>Sets the host's clock to <paramref name="hours"/> after the known epoch.</summary>
    public void At(double hours) => (host.App.Services.GetRequiredService<TimeProvider>() as FakeTimeProvider)!.SetUtcNow(KnownInstants.Epoch.AddHours(hours));   // why: the golden host runs on a FakeTimeProvider

    public Task Start(string address, double hours, string state = "produce")
    {
        At(hours);
        return Row("workflow.started", address, string.Empty, state, "start", Delta(started: true));
    }

    public Task Move(string address, double hours, string from, string to, int? tokens = null)
    {
        At(hours);
        return Row("workflow.transition", address, from, to, "step.completed", Delta(tokens: tokens));
    }

    public Task End(string address, double hours, string from, string to, string outcome)
    {
        At(hours);
        return Row("workflow.transition", address, from, to, "step.completed", Delta(outcome: outcome));
    }

    public Task Park(string address, double hours, string state, ParkReason reason)
    {
        At(hours);
        return Row("workflow.transition", address, state, state, "step.failed", Delta(suspension: new SuspensionSummary(SuspensionKind.Park, reason, ParkKind.Step, Escalation: null, Mirrors: null)));
    }

    public Task Answer(string address, double hours, string state)
    {
        At(hours);
        return Row("workflow.transition", address, state, state, "answer", Delta(), AnswerVariant.Resume);
    }

    private WorkflowEventDelta Delta(bool started = false, int? tokens = null, string? outcome = null, SuspensionSummary? suspension = null) =>
        new(Frame: null, suspension, PendingPark: null, Loops: null, tokens, TokensGranted: null, Facts: null, outcome) { Recipe = started ? recipe : null, RecipeHash = started ? hash : null };

    private Task Row(string type, string address, string from, string to, string trigger, WorkflowEventDelta delta, AnswerVariant? answer = null)
    {
        var detail = new WorkflowEventDetail(TriggerKey.For(WorkflowInstanceId.Of(InstanceAddress.From(address)), "t"), "human:owner", SourceSequence: null, ArgumentsSha256: null, ReportSha256: null, answer, Amount: null, Reason: null,
            Gates: [], Evidence: [], Grant: null, Override: null, Refusal: null, Effects: [], delta);
        var id = WorkflowInstanceId.Of(InstanceAddress.From(address)).Value;
        return ViewScenarios.Append(host, type, new WorkflowEventPayload(id, from, to, trigger, delta.Outcome, Run: null, detail), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"workflow:{address}:{++_next}:{_next}"), TestContext.Current.CancellationToken);
    }
}
