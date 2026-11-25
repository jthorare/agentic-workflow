using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox.Plan;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Resources;

[Lane(TestLane.Unit)]
public sealed class SandboxMemoryShortFormTests
{
    private const string Root = "/home/x/resources";

    [Fact]
    public void A_memory_the_validator_passes_plans_as_the_canonical_size()
    {
        var resources = new GraphResources(Database: null, Sandbox: new GraphSandbox(Memory: "4g"));

        var validation = Validator(boxCap: new ResourceBoxCap(8L << 30, TimeSpan.FromHours(2)))
            .Validate(SpecWith(resources, [Trigger("daily"), Notify("tell", "hi")], [Edge("daily", "tell")]), GraphMode.Activate);
        var plan = ResourcePlanner.Plan(ResourceOwner.Parse("workflow:triage").Value, GraphResourceSpec.From(resources), Root);

        Assert.DoesNotContain(validation.Findings, f => f.Code.StartsWith("SND-RES", StringComparison.Ordinal));
        Assert.False(plan.IsError);
        Assert.Equal("4GiB", plan.Value.Limits!.Memory);
    }

    [Theory]
    [InlineData("3k")]
    [InlineData("5m")]
    [InlineData("2g")]
    [InlineData("1t")]
    [InlineData("3KiB")]
    [InlineData("5MiB")]
    [InlineData("2GiB")]
    [InlineData("1TiB")]
    public void Every_size_unit_resolves_to_the_bytes_the_validator_read(string memory)
    {
        Assert.True(ResourceSizes.TryParse(memory, out var bytes));
        var spec = new ResourceSpec([], [], new SandboxResource(Cpu: null, Memory: memory, Wall: null), []);

        var plan = ResourcePlanner.Plan(ResourceOwner.Parse("workflow:triage").Value, spec, Root);

        Assert.False(plan.IsError);
        var limits = LimitsResolver.Resolve(new LimitsInput(new SandboxLimits(Memory: "4TiB", Cpu: "25%"), AttendedSettings: null, Attended: false, StepTimeout: TimeSpan.FromHours(4), LongMax: null, RamBytes: 8L << 40, Cores: 4, Override: plan.Value.Limits));
        Assert.Equal(bytes, limits.MemoryMaxBytes);
    }
}
