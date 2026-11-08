using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Converters;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>What the Watch activity records is read back by a replay: a result recorded before 'Detail' existed has no such field and must still read, with no detail.</summary>
[Lane(TestLane.Unit)]
public sealed class AgentSessionStateRecordedTests
{
    private static readonly LaunchId _launch = LaunchId.From("LCH-" + new string('a', 32));

    [Fact]
    public void A_result_recorded_before_Detail_existed_reads_back_with_no_detail()
    {
        var recorded = DataConverter.Default.PayloadConverter.ToPayload(new PreDetail(AgentSessionEnd.Exit, ExitCode: 1));

        var read = DataConverter.Default.PayloadConverter.ToValue<AgentSessionState>(recorded);

        Assert.Equal(new AgentSessionState(AgentSessionEnd.Exit, 1), read);
        Assert.Null(read.Detail);
    }

    [Fact]
    public void A_result_with_a_detail_round_trips()
    {
        var state = new AgentSessionState(AgentSessionEnd.Exit, 0, "fact:design isn't JSON");

        var read = DataConverter.Default.PayloadConverter.ToValue<AgentSessionState>(DataConverter.Default.PayloadConverter.ToPayload(state));

        Assert.Equal(state, read);
    }

    [Fact]
    public void A_refusal_is_one_bounded_line_and_is_taken_once()
    {
        var refusals = new ReportRefusals();

        refusals.Record(_launch, "first line\n  second\tline " + new string('x', 1000));

        var taken = refusals.Take(_launch);
        Assert.NotNull(taken);
        Assert.StartsWith("first line second line xxx", taken, StringComparison.Ordinal);
        Assert.DoesNotContain('\n', taken);
        Assert.Equal(ReportRefusals.MaxLength, taken.Length);
        Assert.Null(refusals.Take(_launch));
    }

    /// <summary>The shape of AgentSessionState before 'Detail'.</summary>
    private sealed record PreDetail(AgentSessionEnd? EndedAs, int? ExitCode);
}
