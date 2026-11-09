using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class CreateItemBlockTests
{
    private static Task<Aw.Kernel.Result<BlockResult>> Run(CreateItemBlock block, JsonObject config) =>
        block.Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken).AsTask();

    [Fact]
    public async Task The_block_makes_the_item_through_the_port_and_hands_on_its_id()
    {
        var port = new FlowFakes.RecordingItemPort();

        var result = await Run(new CreateItemBlock(port), new JsonObject { ["title"] = "  Fix login  ", ["repo"] = "web", ["body"] = "details" });

        Assert.True(result.IsSuccess);
        Assert.Equal("itm_1", result.Value.Outputs["itemId"]!.GetValue<string>());
        var (title, repo, body, link) = Assert.Single(port.Created);
        Assert.Equal(("Fix login", "web", "details"), (title, repo, body));
        Assert.Null(link);
    }

    [Fact]
    public async Task A_bare_linear_issue_id_becomes_a_record_key_and_a_whole_key_is_kept()
    {
        var port = new FlowFakes.RecordingItemPort();
        var block = new CreateItemBlock(port);

        _ = await Run(block, new JsonObject { ["title"] = "a", ["linearIssue"] = "abc-1" });
        _ = await Run(block, new JsonObject { ["title"] = "b", ["linearIssue"] = "linear:issue/abc-2" });

        Assert.Equal(["linear:issue/abc-1", "linear:issue/abc-2"], port.Created.Select(static c => c.Link!), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_blank_title_a_bad_linear_issue_and_a_port_that_refuses_fail_the_node_with_the_reason()
    {
        var port = new FlowFakes.RecordingItemPort();
        var blank = await Run(new CreateItemBlock(port), new JsonObject { ["title"] = "   " });
        var badLink = await Run(new CreateItemBlock(port), new JsonObject { ["title"] = "x", ["linearIssue"] = "not a key!" });
        var refused = await Run(new CreateItemBlock(new FlowFakes.RecordingItemPort { Fail = SindriErrors.InvalidInput.With("'web' isn't a repo of the profile") }), new JsonObject { ["title"] = "x", ["repo"] = "web" });

        Assert.Contains("title", blank.Error.Message, StringComparison.Ordinal);
        Assert.Contains("linearIssue", badLink.Error.Message, StringComparison.Ordinal);
        Assert.Contains("isn't a repo", refused.Error.Message, StringComparison.Ordinal);
        Assert.Empty(port.Created);
    }

    [Fact]
    public void The_block_takes_a_title_and_optional_repo_body_and_linear_issue()
    {
        var block = new CreateItemBlock(new FlowFakes.RecordingItemPort());

        Assert.Equal("create_item", block.Type);
        Assert.Empty(block.Config.Check(new JsonObject { ["title"] = "t" }));
        Assert.Contains(block.Config.Check(new JsonObject { ["repo"] = "web" }), static p => p.Field == "title");
        Assert.Equal(["itemId"], block.OutputsFor([]).Fields.Keys, StringComparer.Ordinal);
    }
}
