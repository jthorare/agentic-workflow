using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
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
        _recovering = new AgentRecovering(new BlockCatalog([TestBlocks.Step("step.produce")], []), _outputs, _facts);
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
