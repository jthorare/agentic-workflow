using System.Text.Json;
using Aw.Sindri.Toolkit.Testing;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitHostTests
{
    // Built by concatenation so no secret scanner reads a whole token in this source.
    private static readonly string _secret = string.Concat("not-a-real-", "credential-0123456789");

    private readonly Dictionary<string, string> _environment = new(StringComparer.Ordinal) { ["SINDRI_CONNECTION_GITHUB"] = _secret };

    [Fact, Trait("Lane", "unit")]
    public async Task A_stdio_client_lists_and_calls_a_tool_and_gets_a_typed_result()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await ToolkitSession.StartAsync(SampleToolkit.Create(), _environment, ct);

        var tools = await session.Client.ListToolsAsync(cancellationToken: ct);
        var result = await session.Client.CallToolAsync(
            "diff_contracts", new Dictionary<string, object?>(StringComparer.Ordinal) { ["left"] = "a", ["right"] = "b" }, cancellationToken: ct);

        Assert.Equal(["diff_contracts", "explode"], tools.Select(t => t.Name).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.NotEqual(true, result.IsError);
        var response = result.StructuredContent!.Value.Deserialize(SampleJson.Default.DiffResponse);
        Assert.Equal(new DiffResponse("a vs b", 1), response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_missing_a_required_member_is_an_MCP_tool_error_with_the_invalid_call_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await ToolkitSession.StartAsync(SampleToolkit.Create(), _environment, ct);

        var result = await session.Client.CallToolAsync(
            "diff_contracts", new Dictionary<string, object?>(StringComparer.Ordinal) { ["left"] = "a" }, cancellationToken: ct);

        Assert.True(result.IsError);
        Assert.Contains("AW-TOOLKIT-003", Text(result), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_thrown_exception_becomes_an_MCP_tool_error_without_the_stack_or_secrets()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await ToolkitSession.StartAsync(SampleToolkit.Create(), _environment, ct);

        var result = await session.Client.CallToolAsync(
            "explode", new Dictionary<string, object?>(StringComparer.Ordinal) { ["left"] = "a", ["right"] = "b" }, cancellationToken: ct);

        Assert.True(result.IsError);
        var text = Text(result);
        Assert.Contains("AW-TOOLKIT-004", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain(SampleToolkit.StackLine.Trim(), text, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ToolkitException_reaches_the_client_with_its_code_and_without_a_secret()
    {
        var ct = TestContext.Current.CancellationToken;
        var toolkit = Toolkit.Named("scrub")
            .UseJson(SampleJson.Default)
            .Needs(Resources.Connection("github"))
            .Tool<DiffRequest, DiffResponse>("refuse", "Refuses on purpose.", (_, context, _) =>
                throw new ToolkitException(ToolkitErrors.ResourceUnavailable, $"cannot reach the host with {context.Connection("github")}"));
        await using var session = await ToolkitSession.StartAsync(toolkit, _environment, ct);

        var result = await session.Client.CallToolAsync(
            "refuse", new Dictionary<string, object?>(StringComparer.Ordinal) { ["left"] = "a", ["right"] = "b" }, cancellationToken: ct);

        Assert.True(result.IsError);
        var text = Text(result);
        Assert.Contains("AW-TOOLKIT-005", text, StringComparison.Ordinal);
        Assert.Contains("cannot reach the host with [redacted]", text, StringComparison.Ordinal);
        Assert.DoesNotContain(_secret, text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_call_to_an_unknown_tool_is_a_tool_error_with_the_invalid_call_code()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = await ToolkitSession.StartAsync(SampleToolkit.Create(), _environment, ct);

        var result = await session.Client.CallToolAsync("no_such_tool", new Dictionary<string, object?>(StringComparer.Ordinal), cancellationToken: ct);

        Assert.True(result.IsError);
        var text = Text(result);
        Assert.Contains("AW-TOOLKIT-003", text, StringComparison.Ordinal);
        Assert.Contains("no_such_tool", text, StringComparison.Ordinal);
    }

    private static string Text(CallToolResult result) =>
        string.Join('\n', result.Content.OfType<TextContentBlock>().Select(b => b.Text))
        + (result.StructuredContent?.GetRawText() ?? string.Empty);
}
