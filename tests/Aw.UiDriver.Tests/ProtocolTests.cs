using System.Text;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.UiDriver.Core;
using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Tests;

public sealed class ProtocolTests
{
    private static Result<T> Read<T>(string json)
        where T : notnull => AwJson.Deserialize<T>(Encoding.UTF8.GetBytes(json), UiDriverJson.Options);

    [Fact, Trait("Lane", "unit")]
    public void The_error_catalogue_is_valid() => Assert.True(ErrorCatalogue.Validate(UiDriverErrors.All).IsSuccess);

    [Fact, Trait("Lane", "unit")]
    public void A_request_reads_with_its_defaults_for_what_it_leaves_out()
    {
        var request = Read<WaitForRequest>("""{"selector":{"automationId":"save"}}""").Value;

        Assert.Equal(("save", null, null, null), (request.Selector.AutomationId, request.State, request.Text, request.TimeoutMs));
        Assert.Null(Read<TreeRequest>("{}").Value.From);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_request_round_trips_in_camel_case()
    {
        var request = new TypeRequest("hello", new Selector(AutomationId: "name", Index: 1), Replace: true);

        var json = Encoding.UTF8.GetString(AwJson.Serialize(request, UiDriverJson.Options));

        Assert.Contains("\"automationId\":\"name\"", json, StringComparison.Ordinal);
        Assert.Equal(request, Read<TypeRequest>(json).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_request_with_a_missing_required_field_or_an_unknown_selector_key_is_refused()
    {
        Assert.True(Read<SearchRequest>("{}").IsError);
        Assert.True(Read<ClickRequest>("""{"selector":{"colour":"red"}}""").IsError);
        Assert.True(Read<KeyRequest>("{}").IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tree_answer_round_trips_with_its_children()
    {
        var leaf = new NodeInfo("0/0", "Button", "save", Name: null, "Save", Visible: true, Enabled: true, X: 1.5, Y: 2, Width: 30, Height: 10, Children: []);
        var root = new NodeInfo("0", "Window", AutomationId: null, "Main", Text: null, Visible: true, Enabled: true, X: 0, Y: 0, Width: 800, Height: 600, Children: [leaf]);

        var back = Read<TreeResponse>(Encoding.UTF8.GetString(AwJson.Serialize(new TreeResponse(root), UiDriverJson.Options))).Value;

        Assert.Equal("Save", Assert.Single(back.Root.Children).Text);
        Assert.Equal(1.5, back.Root.Children[0].X);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_endpoint_file_names_its_schema()
    {
        var json = Encoding.UTF8.GetString(AwJson.Serialize(new DriverEndpoint(DriverEndpoint.SchemaName, "http://127.0.0.1:5000", 42), UiDriverJson.Options));

        Assert.Equal("""{"schema":"aw-ui-driver/1","url":"http://127.0.0.1:5000","pid":42}""", json);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_tree_as_deep_as_the_depth_cap_serializes()
    {
        var node = Leaf("0/0");
        for (var level = 0; level < TreeRequest.MaxDepth; level++)
        {
            node = node with { Children = [node] };
        }

        var json = Encoding.UTF8.GetString(AwJson.Serialize(new TreeResponse(node), UiDriverJson.Options));

        Assert.StartsWith("{\"root\":{\"id\":\"0/0\"", json, StringComparison.Ordinal);
    }

    private static NodeInfo Leaf(string id) => new(id, "Border", AutomationId: null, Name: null, Text: null, Visible: true, Enabled: true, X: 0, Y: 0, Width: 10, Height: 10, Children: []);
}
