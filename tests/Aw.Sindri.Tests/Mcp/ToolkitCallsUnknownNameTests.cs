using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.BuildToolkit;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Mcp;

/// <summary>#2735: toolkit_calls on a name no toolkit has answers SND-TK-009 like toolkit_start and toolkit_stop, so a typo never reads as "no calls yet"; a known toolkit with no calls still answers an empty list.</summary>
public sealed class ToolkitCallsUnknownNameTests
{
    private static ToolkitRig.FakeStore Store()
    {
        var store = new ToolkitRig.FakeStore();
        store.Items["triage"] = new Toolkit("triage", "/tk/triage", ToolkitState.Off, SourceHash: null, BuiltAt: null, Manifest: null, CreatedBy: "human");
        return store;
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task toolkit_calls_on_an_unknown_name_answers_SND_TK_009_with_the_list_fix()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, extra: services => services.AddSingleton<IToolkitStore>(Store()));
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var unknown = await mcp.Call("toolkit_calls", """{"name":"triag"}""", ct);

        Assert.Equal(true, unknown.IsError);
        Assert.Equal("SND-TK-009", unknown.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Contains("sindri toolkit list", unknown.StructuredContent!.Value.GetRawText(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 60_000), Trait("Lane", "integration")]
    public async Task toolkit_calls_on_a_known_toolkit_with_no_calls_answers_an_empty_list()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct, extra: services => services.AddSingleton<IToolkitStore>(Store()));
        await using var mcp = await McpSession.Open(host, "alpha", ct);

        var known = await mcp.Call("toolkit_calls", """{"name":"triage"}""", ct);

        Assert.NotEqual(true, known.IsError);
        Assert.Equal(0, known.StructuredContent!.Value.GetProperty("calls").GetArrayLength());
    }
}
