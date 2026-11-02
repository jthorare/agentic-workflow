using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'RecoverAgentStep' over a fake output folder and fact store (issue #1000): outputs present, missing, older than the launch, or not provable.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentRecoveringTests
{
    private const string Dir = "/work/app";
    private readonly FakeStepOutputs _outputs = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly AgentRecovering _recovering;

    public AgentRecoveringTests()
    {
        _recovering = new AgentRecovering(new BlockCatalog([TestBlocks.Step("step.produce")], []), _outputs, _facts, PayloadReportChecks.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentRecoverRun Run(DateTimeOffset? since = null) => new(Dir, "step.produce", "1.1", since);

    [Fact]
    public async Task The_outputs_of_the_one_provable_outcome_are_stored_and_reported()
    {
        _outputs.Add(Dir, "output", """{"found":true}""");

        var recovered = await _recovering.Recover(Run(), Token);

        Assert.Equal("done", recovered.Outcome);
        var stored = Assert.Single(recovered.Outputs);
        Assert.Equal(FactKey.From("fact:output"), stored.Key);
        Assert.Equal("""{"found":true}""", System.Text.Encoding.UTF8.GetString((await _facts.Read(stored.Value, Token)).Value.Span));
        Assert.Equal(64, recovered.Sha!.Length);
        Assert.Null(recovered.Reason);
    }

    /// <summary>Issue 1468: recovery is a second way into a Step's outputs, so it applies the check 'complete' applies to a payload fact: an output that is not a JSON object or array is not accepted.</summary>
    [Fact]
    public async Task A_payload_output_that_is_not_json_is_not_recovered_and_names_the_fact()
    {
        _outputs.Add(Dir, "output", "not json");

        var recovered = await _recovering.Recover(Run() with { Payloads = [FactKey.From("fact:output")] }, Token);

        Assert.Null(recovered.Outcome);
        Assert.Contains("fact:output", recovered.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_payload_output_that_is_a_json_object_is_still_recovered()
    {
        _outputs.Add(Dir, "output", """{"found":true}""");

        var recovered = await _recovering.Recover(Run() with { Payloads = [FactKey.From("fact:output")] }, Token);

        Assert.Equal("done", recovered.Outcome);
    }

    /// <summary>Issue 1468: recovery asks the same checks 'complete' asks (a schema included), through the one shared validator; any refusal means nothing is recovered.</summary>
    [Fact]
    public async Task An_output_the_shared_checks_refuse_is_not_recovered_and_the_refusal_is_the_reason()
    {
        _outputs.Add(Dir, "output", """{"found":true}""");
        var recovering = new AgentRecovering(new BlockCatalog([TestBlocks.Step("step.produce")], []), _outputs, _facts, new RefusingChecks());

        var recovered = await recovering.Recover(Run(), Token);

        Assert.Null(recovered.Outcome);
        Assert.Contains("fact:output breaks its schema", recovered.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_shared_checker_applies_a_facts_schema_as_well_as_the_payload_type()
    {
        var outputs = new Dictionary<FactKey, ReadOnlyMemory<byte>> { [FactKey.From("fact:scopeMap")] = System.Text.Encoding.UTF8.GetBytes("""{"bogus":true}""") };

        var wrong = new ReportOutputChecker().Violations(outputs, new HashSet<FactKey> { FactKey.From("fact:scopeMap") }, scopingQuestions: false);

        Assert.NotEmpty(wrong);
        Assert.Contains("fact:scopeMap", wrong[0], StringComparison.Ordinal);
    }

    private sealed class RefusingChecks : IReportOutputChecks
    {
        public IReadOnlyList<string> Violations(IReadOnlyDictionary<FactKey, ReadOnlyMemory<byte>> outputs, IReadOnlySet<FactKey> payloads, bool scopingQuestions) => ["fact:output breaks its schema"];
    }

    [Fact]
    public async Task A_run_directory_is_where_the_outputs_are_read_from()
    {
        _outputs.Add("/home/runs/WFI-1/draft/LCH-1", "output", """{"found":true}""");

        var recovered = await _recovering.Recover(Run() with { RunDirectory = "/home/runs/WFI-1/draft/LCH-1" }, Token);

        Assert.Equal("done", recovered.Outcome);
        Assert.Equal(FactKey.From("fact:output"), Assert.Single(recovered.Outputs).Key);
    }

    [Fact]
    public async Task A_run_directory_that_is_set_is_the_only_place_read_so_a_worktree_file_is_not_used()
    {
        _outputs.Add(Dir, "output", "in the worktree");

        var recovered = await _recovering.Recover(Run() with { RunDirectory = "/home/runs/WFI-1/draft/LCH-1" }, Token);

        Assert.Null(recovered.Outcome);
    }

    [Fact]
    public async Task A_parked_report_names_the_run_directorys_output_folder()
    {
        _outputs.Add("/home/runs/WFI-1/draft/LCH-1", "scratch.txt", "x");

        var recovered = await _recovering.Recover(Run() with { RunDirectory = "/home/runs/WFI-1/draft/LCH-1" }, Token);

        Assert.StartsWith("the agent finished but its result was not recorded: " + Path.Combine("/home/runs/WFI-1/draft/LCH-1", ".sindri-out"), recovered.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_or_absent_folder_recovers_nothing_so_the_Step_fails_with_the_session_s_own_end()
    {
        var recovered = await _recovering.Recover(Run(), Token);

        Assert.Null(recovered.Outcome);
        Assert.Null(recovered.Reason);
    }

    [Fact]
    public async Task Files_that_are_not_the_Step_s_outputs_park_with_the_folder_named()
    {
        _outputs.Add(Dir, "scratch.txt", "x");

        var recovered = await _recovering.Recover(Run(), Token);

        Assert.Null(recovered.Outcome);
        Assert.StartsWith("the agent finished but its result was not recorded: " + Path.Combine(Dir, ".sindri-out"), recovered.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_output_older_than_the_launch_belongs_to_an_earlier_Step_and_is_not_used()
    {
        var launched = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        _outputs.Add(Dir, "output", "old", launched.AddHours(-1));

        var recovered = await _recovering.Recover(Run(launched), Token);

        Assert.Null(recovered.Outcome);
        Assert.Null(recovered.Reason);
    }

    [Fact]
    public async Task The_same_report_has_the_same_digest()
    {
        _outputs.Add(Dir, "output", "a");

        var first = await _recovering.Recover(Run(), Token);
        var again = await _recovering.Recover(Run(), Token);

        Assert.Equal(first.Sha, again.Sha);
    }
}
