using System.Text.Json.Nodes;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Blocks;

/// <summary>A Step's login-source ask lives on its descriptor, left out when absent so every existing descriptor hash holds (sandbox spec SB18, owner note 4).</summary>
[Lane(TestLane.Unit)]
public sealed class BlockDescriptorLoginSourceTests
{
    private static readonly BlockDescriptor _produce = TestBlocks.Step("step.produce");

    [Fact]
    public void An_absent_ask_is_left_out_of_the_JSON_so_the_hash_is_the_one_before_the_field_existed()
    {
        var node = JsonNode.Parse(AwJson.Serialize(_produce, SliceHarness.Json))!.AsObject();   // why: a descriptor serializes as an object

        Assert.False(node.ContainsKey("loginSource"));
        Assert.Equal(PinRules.Hash(_produce, SliceHarness.Json), PinRules.Hash(_produce with { LoginSource = null }, SliceHarness.Json));
    }

    [Fact]
    public void No_shipped_block_asks_for_a_login_source()
    {
        var asking = FixtureCatalog.Shipped().Blocks.Where(static b => b.LoginSource is not null).Select(static b => b.Id.Value);

        Assert.Empty(asking);
    }

    [Fact]
    public void A_set_ask_is_written_and_changes_the_hash()
    {
        var asking = _produce with { LoginSource = ModelLoginSource.SeparateLogin };

        var node = JsonNode.Parse(AwJson.Serialize(asking, SliceHarness.Json))!.AsObject();   // why: as above

        Assert.Equal("separate-login", node["loginSource"]?.GetValue<string>());
        Assert.NotEqual(PinRules.Hash(_produce, SliceHarness.Json), PinRules.Hash(asking, SliceHarness.Json));
    }
}
