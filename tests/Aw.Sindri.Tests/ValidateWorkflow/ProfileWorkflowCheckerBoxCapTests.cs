using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Graph.Blocks;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.ValidateWorkflow;

/// <summary>The checker the app activates workflows with takes the box cap from the profile's 'sandbox.limits' (SND-RES-005, #2531).</summary>
[Lane(TestLane.Unit)]
public sealed class ProfileWorkflowCheckerBoxCapTests
{
    private static readonly CancellationToken _token = TestContext.Current.CancellationToken;

    private static async Task<GraphValidation> Activate(SindriProfile profile, string memory)
    {
        var profiles = new InMemoryProfiles { Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml") };
        var catalog = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"));
        var validator = await new ProfileWorkflowChecker(profiles, catalog).Validator(_token);
        var spec = SpecWith(new GraphResources(Database: null, Sandbox: new GraphSandbox(Memory: memory)), [Trigger("daily"), Notify("tell", "hi")], [Edge("daily", "tell")]);
        return validator.Validate(spec, GraphMode.Activate);
    }

    [Fact]
    public async Task Activate_refuses_a_sandbox_memory_above_the_profile_limit_with_SND_RES_005()
    {
        var profile = new SindriProfile(1, "thor", Sandbox: new SandboxSettings(Limits: new SandboxLimits(Memory: "8GiB")));

        var result = await Activate(profile, "16g");

        var finding = Assert.Single(result.Findings, f => f.Code == "SND-RES-005");
        Assert.Contains("8GiB", finding.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Activate_passes_a_sandbox_memory_within_the_profile_limit()
    {
        var profile = new SindriProfile(1, "thor", Sandbox: new SandboxSettings(Limits: new SandboxLimits(Memory: "8GiB")));

        Assert.True((await Activate(profile, "4g")).Passed);
    }

    [Fact]
    public async Task Activate_has_no_cap_when_the_profile_has_no_limits()
    {
        var profile = new SindriProfile(1, "thor");

        Assert.True((await Activate(profile, "64g")).Passed);
    }

    [Theory]
    [InlineData("8g")]
    [InlineData("lots")]
    [InlineData("GiB")]
    public async Task Activate_has_no_cap_when_the_profile_limit_is_not_a_size(string limit)
    {
        var profile = new SindriProfile(1, "thor", Sandbox: new SandboxSettings(Limits: new SandboxLimits(Memory: limit)));

        Assert.True((await Activate(profile, "64g")).Passed);
    }
}
