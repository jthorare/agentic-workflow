using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>
/// LN-WF7, long block Steps: the Steps that run the repo's commands declare a 'maxDuration' above the two-minute default and within the 2 h cap ('workflow.steps.longMax' default), run on the
/// block runtime (whose activity heartbeats), and sit in states whose timeout is longer than the Step may run, so the Step's own 'timeout' ends a run before the state's timer does (RCP-TIME-003).
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LongBlockStepsTests
{
    private static readonly TimeSpan _cap = TimeSpan.FromHours(2);

    public static TheoryData<string, string, string> LongSteps => new()
    {
        { "step.build.red-check", "workflow.build.yaml", "red-check" },
        { "step.build.checks", "workflow.build.yaml", "checks" },
        { "step.ship.green", "workflow.ship.yaml", "green" },
        { "step.prove.before-after", "workflow.prove.yaml", "before-after" },
    };

    public static TheoryData<string> Ids => ["step.build.red-check", "step.build.checks", "step.ship.green"];

    // before-after runs the recorded scripts (20 minutes each, at the base and at the head) rather than the repo's commands, so it has no command budget and its own limit
    public static TheoryData<string> AllLongIds => ["step.build.red-check", "step.build.checks", "step.ship.green", "step.prove.before-after"];

    [Theory]
    [MemberData(nameof(AllLongIds))]
    public void A_long_Step_runs_on_the_block_runtime_with_a_maxDuration_above_the_default_and_within_the_cap(string step)
    {
        using var provider = ShippedComposition.Compose();
        var descriptor = provider.GetRequiredService<BlockCatalog>().Find(BlockId.From(step))!;

        Assert.Equal(BlockRuntime.Block, descriptor.Runtime);
        Assert.InRange(BlockLimits.MaxDuration(descriptor), BlockLimits.Fallback + TimeSpan.FromSeconds(1), _cap);
    }

    [Theory]
    [MemberData(nameof(LongSteps))]
    public void The_state_that_runs_a_long_Step_times_out_after_the_Step_may_run(string step, string file, string state)
    {
        using var provider = ShippedComposition.Compose();
        var descriptor = provider.GetRequiredService<BlockCatalog>().Find(BlockId.From(step))!;
        var recipe = RecipeComposition.ReadClean(ShippedRecipes.Read("sindri/" + file), file).Recipe;

        Assert.Equal(step, recipe.States[state].Step!.Use.Value);
        Assert.True(recipe.States[state].Timeout!.After.Span > BlockLimits.MaxDuration(descriptor));
    }

    [Fact]
    public void Before_after_may_run_two_of_its_scripts_end_to_end_in_one_run()
    {
        using var provider = ShippedComposition.Compose();
        var max = BlockLimits.MaxDuration(provider.GetRequiredService<BlockCatalog>().Find(BlockId.From("step.prove.before-after"))!);

        Assert.True(max >= (2 * TimeSpan.FromMinutes(20)) + TimeSpan.FromMinutes(1));   // one signal: its script at the base and at the head, each up to the script timeout
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void The_commands_budget_is_inside_the_Steps_MaxDuration_with_room_to_write_the_report(string step)
    {
        using var provider = ShippedComposition.Compose();
        var max = BlockLimits.MaxDuration(provider.GetRequiredService<BlockCatalog>().Find(BlockId.From(step))!);

        Assert.True(RepoCommandRunner.BudgetFor(max) <= max - TimeSpan.FromMinutes(1));
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void A_Step_may_run_for_the_default_longMax_so_a_one_hour_suite_fits_after_the_longest_slot_wait(string step)
    {
        using var provider = ShippedComposition.Compose();
        var max = BlockLimits.MaxDuration(provider.GetRequiredService<BlockCatalog>().Find(BlockId.From(step))!);

        Assert.Equal(_cap, max);
        Assert.True(RepoCommandRunner.BudgetFor(max) - TimeSpan.FromMinutes(30) >= TimeSpan.FromHours(1));
    }
}
