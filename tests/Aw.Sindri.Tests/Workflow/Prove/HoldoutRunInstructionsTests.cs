using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Line;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>What 'step.prove.holdout' is told (line spec §9.3, schemas/line/holdout-result.json, #1055): the evaluator's brief names the result it writes and the shape 'step.prove.holdout-log' reads.</summary>
[Lane(TestLane.Unit)]
public sealed class HoldoutRunInstructionsTests
{
    private static string All
    {
        get
        {
            using var provider = new ServiceCollection().AddLineBlockRuntimes().BuildServiceProvider();
            var step = BlockId.From("step.prove.holdout");
            var found = provider.GetServices<IAgentStepInstructions>().Where(i => i.IsFor(step)).ToList();
            return string.Join('\n', found.SelectMany(static i => i.Lines));
        }
    }

    [Fact]
    public void The_holdout_run_Step_has_registered_instructions() => Assert.False(string.IsNullOrEmpty(All));

    [Theory]
    [InlineData("fact:holdoutResult")]
    [InlineData("scenarios")]
    [InlineData("\"id\"")]
    [InlineData("\"item\"")]
    [InlineData("\"verdict\"")]
    [InlineData("pass")]
    [InlineData("fail")]
    [InlineData("\"category\"")]
    [InlineData("\"observed\"")]
    [InlineData("200")]
    [InlineData("\"head\"")]
    [InlineData("set.json")]
    [InlineData("read-only")]
    public void The_instructions_name_the_result_file_and_its_required_fields(string phrase) =>
        Assert.Contains(phrase, All, StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void The_instructions_forbid_quoting_the_scenario_in_the_observed_line() =>
        Assert.Contains("never", All, StringComparison.OrdinalIgnoreCase);
}
