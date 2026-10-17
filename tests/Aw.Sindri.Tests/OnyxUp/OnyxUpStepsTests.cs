using Aw.Kernel;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxUp;

// The step list is built and checked in one place; each step names its upstream and gets its stamp, and the response says what each step did.
public sealed class OnyxUpStepsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class StubStep(string name, OnyxStepOutcome outcome = OnyxStepOutcome.Ran) : IOnyxUpStep
    {
        public string Name => name;

        public string Stamp(OnyxUpContext context, string? upstream) => name + ":" + upstream;

        public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<OnyxStepOutcome>>(outcome);
    }

    private static OnyxUpService Service(OnyxUpRig rig, params IOnyxUpStep[] steps)
        => new(rig.Workspace, new OnyxFacts(rig.Runtime, rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), rig.Pins, rig.Progress, steps: steps);

    [Fact, Trait("Lane", "unit")]
    public void The_steps_are_fetch_config_pull_start()
    {
        var rig = new OnyxUpRig();

        _ = rig.Service();   // composes, so the order is validated

        Assert.Equal(["fetch", "config", "pull", "start"], OnyxSteps.Order, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_list_out_of_order_or_missing_a_step_does_not_compose()
    {
        var rig = new OnyxUpRig();
        var fetch = new StubStep("fetch");
        var config = new StubStep("config");
        var pull = new StubStep("pull");
        var start = new StubStep("start");

        _ = Assert.Throws<InvalidOperationException>(() => Service(rig, config, fetch, pull, start));
        _ = Assert.Throws<InvalidOperationException>(() => Service(rig, fetch, config, start));
        _ = Service(rig, fetch, config, pull, start);
    }

    private static (OnyxUpRig Rig, OnyxUpContext Context, IOnyxUpStep[] Steps) Chain()
    {
        var rig = new OnyxUpRig();
        var compose = new Infrastructure.Onyx.YamlComposeServices();
        IOnyxUpStep[] steps =
        [
            new OnyxFetchStep(rig.Files, rig.Progress),
            new OnyxConfigStep(rig.Workspace, compose, rig.Random, rig.Progress),
            new OnyxPullStep(rig.Runtime, rig.Workspace, compose, rig.Progress),
            new OnyxStartStep(rig.Runtime, rig.Health, new OnyxReadiness(rig.Health, rig.Runtime, rig.Clock), rig.Progress, rig.Clock),
        ];
        return (rig, new OnyxUpContext(rig.Pins.Pins, rig.Paths, 3000, "linux/arm64"), steps);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_steps_stamp_changes_when_its_upstream_stamp_changes()
    {
        var (_, context, steps) = Chain();

        foreach (var step in steps.Skip(1))
        {
            Assert.NotEqual(step.Stamp(context, "upstream-a"), step.Stamp(context, "upstream-b"), StringComparer.Ordinal);
            Assert.Equal(step.Stamp(context, "upstream-a"), step.Stamp(context, "upstream-a"), StringComparer.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void The_first_step_has_no_upstream_and_every_later_step_without_one_is_an_error()
    {
        var (_, context, steps) = Chain();

        Assert.NotEmpty(steps[0].Stamp(context, upstream: null));
        foreach (var step in steps.Skip(1))
        {
            var error = Assert.Throws<InvalidOperationException>(() => step.Stamp(context, upstream: null));
            Assert.Contains(step.Name, error.Message, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_plan_hands_each_step_the_stamp_of_the_one_before_it()
    {
        var rig = new OnyxUpRig();
        var seen = new List<string?>();
        var service = Service(rig, new Recording("fetch", seen), new Recording("config", seen), new Recording("pull", seen), new Recording("start", seen));

        _ = (await service.Handle(new OnyxUpRequest(), Token)).Value;

        Assert.Equal([null, "fetch:", "config:fetch:", "pull:config:fetch:"], seen);
    }

    private sealed class Recording(string name, List<string?> seen) : IOnyxUpStep
    {
        public string Name => name;

        public string Stamp(OnyxUpContext context, string? upstream)
        {
            seen.Add(upstream);
            return name + ":" + upstream;
        }

        public ValueTask<Result<OnyxStepOutcome>> Execute(OnyxUpContext context, string stamp, string? recorded, CancellationToken cancellationToken)
            => ValueTask.FromResult<Result<OnyxStepOutcome>>(OnyxStepOutcome.Ran);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_response_carries_one_typed_outcome_per_step_in_order()
    {
        var rig = new OnyxUpRig();

        var first = (await rig.Up(Token)).Value;
        var second = (await rig.Up(Token)).Value;

        Assert.Equal(
            [new OnyxStepResult("fetch", OnyxStepOutcome.Ran), new OnyxStepResult("config", OnyxStepOutcome.Ran), new OnyxStepResult("pull", OnyxStepOutcome.Ran), new OnyxStepResult("start", OnyxStepOutcome.Ran)],
            first.Steps);
        Assert.All(second.Steps, static s => Assert.Equal(OnyxStepOutcome.Skipped, s.Outcome));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_step_that_needs_the_human_is_reported_as_such_and_is_recorded()
    {
        var rig = new OnyxUpRig();
        var service = Service(rig, new StubStep("fetch"), new StubStep("config"), new StubStep("pull"), new StubStep("start", outcome: OnyxStepOutcome.NeedsHuman));

        var response = (await service.Handle(new OnyxUpRequest(), Token)).Value;

        Assert.Equal(OnyxStepOutcome.NeedsHuman, response.Steps[^1].Outcome);
        Assert.Contains("start", response.NeedsHuman, StringComparer.Ordinal);
    }
}
