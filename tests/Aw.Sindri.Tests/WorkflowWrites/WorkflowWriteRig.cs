using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Expressions;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Schedule;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.Toolkits;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.WorkflowWrites;

/// <summary>The ports the workflow write slices share: the in-memory workflow store, the lease and ledger of <see cref="SliceHarness"/>, a caller, and a checker over the real block types or the small fixture ones.</summary>
internal sealed class WorkflowWriteRig : IDisposable
{
    public static readonly WorkflowId Id = WorkflowId.From("WF-00000000000000000000000000000001");
    public static readonly WorkflowSlug Slug = WorkflowSlug.From("drift-check");

    private readonly IDisposable _caller;

    public WorkflowWriteRig(bool realBlocks = false, bool flowAndCodeBlocks = false)
    {
        Harness = new SliceHarness();
        Workflows = new InMemoryWorkflowStore(Harness.Clock);
        Checker = new FixedChecker(realBlocks ? RealValidator(flowAndCodeBlocks) : Validator());
        Log = new WorkflowChangeLog(Harness.Store, Harness.Held, Harness.Events, Harness.Callers);
        _caller = Harness.Callers.EnterService("workflow-write-test");
    }

    public SliceHarness Harness { get; }

    public InMemoryWorkflowStore Workflows { get; }

    public IWorkflowChecker Checker { get; }

    public WorkflowChangeLog Log { get; }

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>daily -> a -> tell, over the fixture block types.</summary>
    public static GraphSpec SampleGraph() => Spec([Trigger("daily"), Search("a"), Notify("tell", "{{a.facts}}")], [Edge("daily", "a"), Edge("a", "tell")]);

    /// <summary>The ledger events of one type, oldest first.</summary>
    public IReadOnlyList<LedgerEvent> Recorded(string type) => [.. Harness.Store.Events.Where(e => string.Equals(e.Envelope.Type, type, StringComparison.Ordinal))];

    /// <summary>Makes the workflow at revision 1 by the owner.</summary>
    public async Task Seed()
    {
        _ = await Harness.HoldLease();
        _ = (await Workflows.Create(Id, Slug, SampleGraph(), "owner", Token)).Value;
    }

    public void Dispose() => _caller.Dispose();

    // flowAndCodeBlocks adds create_item and code, which the starter hello-sindri uses and the standard set leaves out (the host registers them with their ports).
    // call_tool is always in: the host registers it, and a starter made with a CI source has a call_tool step (#3534)
    private static GraphValidator RealValidator(bool flowAndCodeBlocks) => Validator(
        blocks: GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"), more: flowAndCodeBlocks ? [CallTool(), new CreateItemBlock(new FlowFakes.RecordingItemPort()), new CodeBlock(runner: null!)] : [CallTool()]).Types,
        cron: (cron, zone) => CronExpression.TryParse(cron, zone, out _, out var problem) ? null : problem,
        condition: text => ConditionExpression.TryParse(text, out var expression, out var error) ? new ConditionParse(Error: null, expression!.References) : new ConditionParse(error, []));

    private static CallToolBlock CallTool() => new(new FakeRunningToolkits([]), new NoConnections());

    private sealed class FixedChecker(GraphValidator validator) : IWorkflowChecker
    {
        public ValueTask<GraphValidator> Validator(CancellationToken cancellationToken) => ValueTask.FromResult(validator);
    }

    // why: validation never calls a connection; every call is refused
    private sealed class NoConnections : IConnectionToolPort
    {
        public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<IReadOnlyList<ConnectionTool>>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));

        public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<string>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));
    }
}
