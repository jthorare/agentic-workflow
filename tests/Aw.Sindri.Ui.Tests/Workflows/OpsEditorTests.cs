using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>The workflow editor's logic (#2258 PR 17 part 2b): its form comes from the block list, each edit is one set of ops on the revision it read, and a workflow that changed elsewhere asks for a reload.</summary>
[Lane(TestLane.Unit)]
public sealed class OpsEditorTests
{
    // 'found' is at revision 2 (latestRevision) with nodes start (manual) and agent (run_agent, prompt "Look for drift")
    private const string Blocks = """
        {"groups":[
          {"group":"Triggers","blocks":[{"type":"manual","title":"Manual","isTrigger":true,"config":[],"outputs":{},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]},
          {"group":"AI","blocks":[{"type":"run_agent","title":"Run an agent","isTrigger":false,
            "config":[{"name":"prompt","type":"text","required":true},{"name":"retries","type":"number","required":false},{"name":"tags","type":"text list","required":false},{"name":"strict","type":"boolean","required":false},{"name":"env","type":"object","required":false}],
            "outputs":{"text":"text"},"branches":["out"],"exampleConfig":null,"exampleSpec":null}]}
        ]}
        """;

    private readonly WorkflowsFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<OpsEditor> Loaded()
    {
        _f.Server.WorkflowBlocksAnswer = new RouteAnswer(HttpStatusCode.OK, Blocks);
        var editor = new OpsEditor(_f.Server);
        await editor.Load(Url, Slug, Token);
        editor.Select("agent");
        return editor;
    }

    private void AssertSent(int index, string ops, int baseRevision = 2)
    {
        var sent = _f.Server.AppliedWorkflowOps[index];
        Assert.Equal((Slug, baseRevision), (sent.Slug, sent.BaseRevision));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(ops), JsonNode.Parse(sent.Ops)), sent.Ops);
    }

    [Fact]
    public async Task Editing_a_config_field_sends_set_config_with_the_base_revision_and_a_conflict_prompts_a_reload()
    {
        var editor = await Loaded();

        await editor.EditField("prompt", "Check twice", Token);

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        AssertSent(0, """[{"op":"set_config","node":"agent","path":"prompt","value":"Check twice"}]""");
        Assert.Equal((false, string.Empty), (editor.NeedsReload, editor.ReloadPromptText));

        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-stale");
        await editor.EditField("prompt", "Check thrice", Token);

        Assert.Equal(2, _f.Server.AppliedWorkflowOps.Count);   // sent once: a stale base is never retried
        Assert.Equal((true, "Reload", string.Empty), (editor.NeedsReload, editor.ReloadText, editor.FailureText));
        Assert.StartsWith("This workflow changed elsewhere", editor.ReloadPromptText, StringComparison.Ordinal);

        await editor.EditField("prompt", "Check again", Token);
        Assert.Equal(2, _f.Server.AppliedWorkflowOps.Count);   // held back until the reload

        var reads = _f.Server.WorkflowCalls;
        await editor.Reload(Token);

        Assert.Equal(reads + 1, _f.Server.WorkflowCalls);
        Assert.Equal((false, string.Empty), (editor.NeedsReload, editor.ReloadPromptText));
    }

    [Fact]
    public async Task The_form_has_the_fields_of_the_selected_nodes_block_with_the_values_it_has()
    {
        var editor = await Loaded();

        Assert.Equal(["prompt", "retries", "tags", "strict", "env"], editor.Fields.Select(f => f.Name), StringComparer.Ordinal);
        Assert.Equal(("text", true, "Look for drift"), (editor.Fields[0].Type, editor.Fields[0].IsRequired, editor.Fields[0].Text));
        Assert.Equal((false, string.Empty), (editor.Fields[1].IsRequired, editor.Fields[1].Text));
        Assert.Equal(["Triggers", "AI"], editor.Groups.Select(g => g.Group), StringComparer.Ordinal);
        Assert.Equal((2, "start", "manual"), (editor.BaseRevision, editor.Nodes[0].Id, editor.Nodes[0].Type));

        editor.Select("start");

        Assert.Empty(editor.Fields);
    }

    [Theory]
    [InlineData("retries", "3", """[{"op":"set_config","node":"agent","path":"retries","value":3}]""")]
    [InlineData("strict", "true", """[{"op":"set_config","node":"agent","path":"strict","value":true}]""")]
    [InlineData("tags", "a\nb\n\n", """[{"op":"set_config","node":"agent","path":"tags","value":["a","b"]}]""")]
    [InlineData("env", """{"K":"v"}""", """[{"op":"set_config","node":"agent","path":"env","value":{"K":"v"}}]""")]
    [InlineData("retries", "", """[{"op":"set_config","node":"agent","path":"retries","value":null}]""")]
    public async Task A_field_is_sent_as_its_type(string field, string text, string ops)
    {
        var editor = await Loaded();

        await editor.EditField(field, text, Token);

        AssertSent(0, ops);
    }

    [Theory]
    [InlineData("retries", "many", "Enter a number.")]
    [InlineData("strict", "maybe", "Enter true or false.")]
    [InlineData("env", "[1]", """Enter a JSON object, such as {"key": "value"}.""")]
    [InlineData("prompt", "", "This field is required.")]
    public async Task Text_that_does_not_fit_its_field_is_not_sent_and_says_why(string field, string text, string line)
    {
        var editor = await Loaded();

        await editor.EditField(field, text, Token);

        Assert.Empty(_f.Server.AppliedWorkflowOps);
        var shown = editor.Fields.Single(f => string.Equals(f.Name, field, StringComparison.Ordinal));
        Assert.Equal((line, true), (shown.ErrorText, shown.HasError));
    }

    [Fact]
    public async Task Add_node_connect_and_remove_node_send_their_ops_and_leave_out_what_is_not_given()
    {
        var editor = await Loaded();

        await editor.AddNode("notify", "notify", title: null, Token);
        await editor.Connect("agent", "notify", output: null, Token);
        await editor.Connect("agent", "notify", "out", Token);
        await editor.RemoveNode("notify", Token);

        AssertSent(0, """[{"op":"add_node","id":"notify","type":"notify"}]""");
        AssertSent(1, """[{"op":"connect","from":"agent","to":"notify"}]""");
        AssertSent(2, """[{"op":"connect","from":"agent","to":"notify","output":"out"}]""");
        AssertSent(3, """[{"op":"remove_node","node":"notify"}]""");
    }

    [Fact]
    public async Task A_title_is_sent_when_given()
    {
        var editor = await Loaded();

        await editor.AddNode("tell", "notify", "Tell me", Token);

        AssertSent(0, """[{"op":"add_node","id":"tell","type":"notify","title":"Tell me"}]""");
    }

    [Fact]
    public async Task A_refusal_that_is_not_a_stale_base_says_why_and_does_not_ask_for_a_reload()
    {
        var editor = await Loaded();
        _f.Server.ApplyWorkflowOpsAnswer = RouteAnswer.Problem("workflows-slug-ops", "ops-missing-fields");

        await editor.EditField("prompt", "x", Token);

        Assert.Equal((false, true), (editor.NeedsReload, editor.FailureText.StartsWith("Couldn't save the edit. ", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_second_edit_while_one_is_on_the_way_does_nothing()
    {
        var editor = await Loaded();
        _f.Server.ApplyWorkflowOpsGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = editor.EditField("prompt", "one", Token);
        await editor.EditField("prompt", "two", Token);
        _f.Server.ApplyWorkflowOpsGate.SetResult();
        await first;

        _ = Assert.Single(_f.Server.AppliedWorkflowOps);
        Assert.False(editor.IsSending);
    }

    [Fact]
    public async Task A_block_list_that_cannot_be_read_says_so_and_the_form_stays_empty()
    {
        _f.Server.WorkflowBlocksAnswer = RouteAnswer.Problem(HttpStatusCode.NotFound, "SND-GRAPH-008", "no blocks served");
        var editor = new OpsEditor(_f.Server);

        await editor.Load(Url, Slug, Token);
        editor.Select("agent");

        Assert.StartsWith("Couldn't read the workflow to edit. ", editor.FailureText, StringComparison.Ordinal);
        Assert.Empty(editor.Fields);
    }
}
