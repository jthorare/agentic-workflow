using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Resources;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.Graph;

[Lane(TestLane.Unit)]
public sealed class GraphValidatorResourcesTests
{
    private static readonly ResourceBoxCap _cap = new(8L << 30, TimeSpan.FromHours(2));

    private static GraphValidation Check(GraphResources resources) =>
        Validator(boxCap: _cap).Validate(SpecWith(resources, [Trigger("daily"), Notify("tell", "hi")], [Edge("daily", "tell")]), GraphMode.Activate);

    [Fact]
    public void A_sandbox_memory_above_the_box_cap_is_refused_naming_the_cap()
    {
        var result = Check(new GraphResources(Database: null, Sandbox: new GraphSandbox(Memory: "16g")));

        var finding = Assert.Single(result.Findings, f => f.Code == "SND-RES-005");
        Assert.Contains("8GiB", finding.ToString(), StringComparison.Ordinal);
        Assert.False(result.Passed);
    }

    [Fact]
    public void A_sandbox_wall_above_the_box_cap_is_refused_naming_the_cap()
    {
        var result = Check(new GraphResources(Database: null, Sandbox: new GraphSandbox(Wall: "3h")));

        var finding = Assert.Single(result.Findings, f => f.Code == "SND-RES-005");
        Assert.Contains("2h", finding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_sandbox_within_the_box_cap_passes()
    {
        var result = Check(new GraphResources(new GraphDatabase("state"), new GraphSandbox(Memory: "4g", Wall: "45m")));

        Assert.True(result.Passed);
    }

    [Theory]
    [InlineData("Bad Name")]
    [InlineData("UPPER")]
    [InlineData("")]
    public void A_database_name_outside_lowercase_letters_digits_and_dashes_is_refused(string name)
    {
        var result = Check(new GraphResources(new GraphDatabase(name), Sandbox: null));

        Assert.Contains(result.Findings, f => f.Code == "SND-RES-006" && f.Field == "resources.database.name");
    }

    [Theory]
    [InlineData("lots")]
    [InlineData("0g")]
    public void A_sandbox_memory_that_is_not_a_size_is_refused(string memory)
    {
        var result = Check(new GraphResources(Database: null, Sandbox: new GraphSandbox(Memory: memory)));

        Assert.Contains(result.Findings, f => f.Code == "SND-RES-006" && f.Field == "resources.sandbox.memory");
    }

    [Fact]
    public void Without_a_box_cap_the_size_is_not_compared()
    {
        var spec = SpecWith(new GraphResources(Database: null, Sandbox: new GraphSandbox(Memory: "16g")), [Trigger("daily"), Notify("tell", "hi")], [Edge("daily", "tell")]);

        Assert.True(Validator().Validate(spec, GraphMode.Activate).Passed);
    }
}
