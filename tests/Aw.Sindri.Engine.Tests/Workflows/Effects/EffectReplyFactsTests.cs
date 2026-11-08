using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Temporalio.Exceptions;
using Temporalio.Testing;

namespace Aw.Sindri.Tests.Workflows.Effects;

/// <summary>
/// An effect's reply may carry facts (forge wiring brief item 3): they ride the reply event's payload, only when the effect's
/// descriptor lists them in 'Outputs', the reply event declares them, and the instance began with the 'forge-reply-facts'
/// marker; any other fact refuses the dispatch before a row is written, so it is never dropped silently.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class EffectReplyFactsTests : IDisposable
{
    private static readonly InstanceAddress _address = InstanceAddress.Root(ItemKey.From("T-82"), RecipeId.From("workflow.dev-notify"), 1);
    private static readonly WorkflowInstanceId _instance = WorkflowInstanceId.From("WFI-" + new string('9', 32));
    private static readonly BlockId _notify = BlockId.From("effect.notify.desk");
    private static readonly FactKey _pr = FactKey.From("fact:pr");
    private static readonly FactKey _other = FactKey.From("fact:other");
    private static readonly ReplyFact _value = new("fact-ref-1", new string('a', 64), HeadSha: null);
    private readonly WorkflowsHarness _harness = new();
    private readonly FakeDeskEffect _desk = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task A_declared_fact_rides_the_reply_and_reaches_the_waiting_frame_as_an_output()
    {
        _desk.ReplyFacts = new Dictionary<FactKey, ReplyFact> { [_pr] = _value };
        await using var sindri = await Compose(effectWrites: [_pr], eventFacts: [_pr]);

        _ = await Run(sindri, replyFacts: true);

        var reply = Assert.Single(await EffectRows.Events(sindri, "notify.delivered"));
        using var payload = JsonDocument.Parse(reply.Envelope.Payload);
        Assert.Equal(_value.Ref, payload.RootElement.GetProperty("facts").GetProperty("fact:pr").GetProperty("ref").GetString());
        var descriptor = sindri.GetRequiredService<BlockCatalog>().FindEvent(EventKind.From("notify.delivered"))!;
        var (trigger, problem) = DeliveredEvents.Trigger(descriptor, reply, "system:dispatcher");
        Assert.Null(problem);
        Assert.Equal(new FactValue(_value.Ref, _value.Sha256, _value.HeadSha), trigger!.Outputs[_pr]);
    }

    [Fact]
    public async Task A_reply_with_no_facts_is_the_payload_it_always_was()
    {
        await using var sindri = await Compose(effectWrites: [_pr], eventFacts: [_pr]);

        _ = await Run(sindri, replyFacts: true);

        var reply = Assert.Single(await EffectRows.Events(sindri, "notify.delivered"));
        Assert.Equal($"{{\"outcome\":\"delivered\",\"effect\":\"{new EffectIdentity(_instance, Entry: 2, Index: 0).Text}\"}}", reply.Envelope.Payload);
    }

    [Fact]
    public async Task A_fact_the_effect_does_not_declare_is_refused_naming_it_and_writes_nothing()
    {
        _desk.ReplyFacts = new Dictionary<FactKey, ReplyFact> { [_pr] = _value, [_other] = _value };
        await using var sindri = await Compose(effectWrites: [_pr], eventFacts: [_pr, _other]);

        var refused = await Assert.ThrowsAsync<ApplicationFailureException>(() => Run(sindri, replyFacts: true));

        await Refusal(sindri, refused, "fact:other");
    }

    [Fact]
    public async Task A_fact_the_reply_event_does_not_declare_is_refused_naming_it_and_writes_nothing()
    {
        _desk.ReplyFacts = new Dictionary<FactKey, ReplyFact> { [_pr] = _value };
        await using var sindri = await Compose(effectWrites: [_pr], eventFacts: []);

        var refused = await Assert.ThrowsAsync<ApplicationFailureException>(() => Run(sindri, replyFacts: true));

        await Refusal(sindri, refused, "fact:pr");
    }

    [Fact]
    public async Task An_instance_that_began_before_the_marker_refuses_a_reply_with_facts()
    {
        _desk.ReplyFacts = new Dictionary<FactKey, ReplyFact> { [_pr] = _value };
        await using var sindri = await Compose(effectWrites: [_pr], eventFacts: [_pr]);

        var refused = await Assert.ThrowsAsync<ApplicationFailureException>(() => Run(sindri, replyFacts: false));

        await Refusal(sindri, refused, "fact:pr");
    }

    private static async Task Refusal(IServiceProvider sindri, ApplicationFailureException refused, string fact)
    {
        Assert.Equal(WorkflowErrors.EffectFailed.Code, refused.ErrorType);
        Assert.True(refused.NonRetryable);
        Assert.Contains(fact, refused.Message, StringComparison.Ordinal);
        Assert.Empty(await EffectRows.Events(sindri, OutboxEvents.Dispatched));
        Assert.Empty(await EffectRows.Events(sindri, "notify.delivered"));
    }

    /// <summary>The host with the desk effect and its reply event re-declared to write <paramref name="effectWrites"/> and <paramref name="eventFacts"/>.</summary>
    private async Task<ServiceProvider> Compose(FactKey[] effectWrites, FactKey[] eventFacts)
    {
        var sindri = _harness.Sindri(services =>
        {
            _desk.Replace(services);
            var blocks = services.Select(static d => d.ImplementationInstance).OfType<BlockDescriptor>().ToList();
            var events = services.Select(static d => d.ImplementationInstance).OfType<EventDescriptor>().ToList();
            _ = services.RemoveAll<BlockDescriptor>();
            _ = services.RemoveAll<EventDescriptor>();
            foreach (var block in blocks)
            {
                _ = services.AddSingleton(block.Id == _notify ? block with { Outputs = [.. effectWrites.Select(static k => new OutputSpec(k, []))] } : block);
            }

            foreach (var reply in events)
            {
                _ = services.AddSingleton(reply.Kind.Value == "notify.delivered" ? reply with { Facts = eventFacts } : reply);
            }
        });
        await WorkflowsHarness.HoldLease(sindri, TestContext.Current.CancellationToken);
        return sindri;
    }

    private static async Task<EffectRunResult> Run(IServiceProvider sindri, bool replyFacts)
    {
        var command = new EffectCommand(_notify, new EffectIdentity(_instance, Entry: 2, Index: 0), CommandOrigin.Recipe, new Dictionary<string, Core.Workflow.Recipes.WithValue>(StringComparer.Ordinal));
        var run = new EffectRun(_address.Value, "T-82", Attempt: 1, command, "cmd-1", Text: null, new Dictionary<FactKey, FactValue>(), ReplyFacts: replyFacts);
        var environment = new ActivityEnvironment { Info = ActivityEnvironment.DefaultInfo with { WorkflowId = _address.Value, WorkflowRunId = "run-1", ActivityId = "effect-1", IsLocal = false } };
        return await environment.RunAsync(() => sindri.GetRequiredService<EffectActivities>().RunAsync(run));
    }
}
