using Aw.Sindri.Tests.ContextCalls;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>sindri_todo's picks and import (todo spec §4.4, Ph3b): each action reaches the service its route calls as a session caller, and its arguments are bound with the route's own rules.</summary>
public sealed class SindriTodoPickToolsTests
{
    private const string NoId = "td_01jc5a8xq0n4w7ms2t9r3vbk6e";

    private static string? Code(CallToolResult result) => result.StructuredContent is { } s && s.TryGetProperty("code", out var code) ? code.GetString() : null;

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Keep_on_a_todo_with_no_conflict_reaches_the_service_and_is_SND_TODO_005()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);
        var id = (await mcp.Call("sindri_todo", """{"action":"add","text":"call the dentist","clientId":"k-1"}""", ct)).StructuredContent!.Value.GetProperty("id").GetString()!;
        await world.Settle(ct);

        var kept = await mcp.Call("sindri_todo", $$"""{"action":"keep","id":"{{id}}","side":"sindri"}""", ct);
        var undone = await mcp.Call("sindri_todo", $$"""{"action":"keepUndo","id":"{{id}}"}""", ct);

        Assert.Equal("SND-TODO-005", Code(kept));
        Assert.Equal("SND-TODO-005", Code(undone));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Keep_on_a_todo_that_does_not_exist_is_SND_TODO_404()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var kept = await mcp.Call("sindri_todo", $$"""{"action":"keep","id":"{{NoId}}","side":"obsidian"}""", ct);

        Assert.Equal("SND-TODO-404", Code(kept));
    }

    [Theory(Timeout = 60_000), Trait("Lane", "integration")]
    [InlineData("""{"action":"keep","id":"td_01jc5a8xq0n4w7ms2t9r3vbk6e"}""")]
    [InlineData("""{"action":"keep","side":"sindri"}""")]
    [InlineData("""{"action":"keepUndo"}""")]
    [InlineData("""{"action":"holdApply"}""")]
    [InlineData("""{"action":"holdRestore"}""")]
    [InlineData("""{"action":"noteImport","vault":"notes"}""")]
    [InlineData("""{"action":"noteImport","note":"Todo.md"}""")]
    [InlineData("""{"action":"noteImport","vault":"notes","note":"Todo.md","expectOpen":-1}""")]
    [InlineData("""{"action":"holdUndo","bogus":1}""")]
    public async Task A_pick_missing_a_required_argument_or_with_a_wrong_one_is_SND_CONTEXT_022(string arguments)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var refused = await mcp.Call("sindri_todo", arguments, ct);

        Assert.Equal("SND-CONTEXT-022", Code(refused));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task A_side_that_is_neither_sindri_nor_obsidian_is_refused_as_the_route_refuses_it()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var refused = await mcp.Call("sindri_todo", $$"""{"action":"keep","id":"{{NoId}}","side":"both"}""", ct);

        Assert.Equal("SND-CLI-003", Code(refused));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Hold_picks_and_their_undo_with_no_hold_open_are_SND_TODO_005()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var apply = await mcp.Call("sindri_todo", """{"action":"holdApply","noteHash":"sha256:3f1c"}""", ct);
        var restore = await mcp.Call("sindri_todo", """{"action":"holdRestore","noteHash":"sha256:3f1c"}""", ct);
        var undo = await mcp.Call("sindri_todo", """{"action":"holdUndo"}""", ct);

        Assert.Equal("SND-TODO-005", Code(apply));
        Assert.Equal("SND-TODO-005", Code(restore));
        Assert.Equal("SND-TODO-005", Code(undo));
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task Note_import_with_no_key_in_the_profile_is_SND_TODO_002()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var world = await McpWorld.Start(ct);
        await using var mcp = await McpSession.Open(world.Host, "alpha", ct);

        var dry = await mcp.Call("sindri_todo", """{"action":"noteImport","vault":"notes","note":"Todo.md"}""", ct);
        var import = await mcp.Call("sindri_todo", """{"action":"noteImport","vault":"notes","note":"Todo.md","expectOpen":2}""", ct);

        Assert.Equal("SND-TODO-002", Code(dry));
        Assert.Equal("SND-TODO-002", Code(import));
    }
}
