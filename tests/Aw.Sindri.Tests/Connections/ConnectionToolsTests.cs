using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Features.GetConnection;
using Aw.Sindri.Features.ListConnections;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Mcp;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Connections;

/// <summary>The connection_* MCP tools, called in process over the in-memory ports. (tools/list is a golden the lead regenerates: five tools were added to the table.)</summary>
[Lane(TestLane.Unit)]
public sealed class ConnectionToolsTests
{
    private const string Access = "access-SECRET-ABCDEF123456";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(ServiceProvider Services, ConnectionHarness World)> Seeded()
    {
        var world = new ConnectionHarness();
        var pointer = world.Secrets.Store(ConnectionSamples.Name(), new TokenSet(Access, "refresh-SECRET-654321", ExpiresAt: null)).Value;
        _ = await world.Store.Upsert(ConnectionSamples.Oauth() with { SecretPointer = pointer }, Token);
        var services = new ServiceCollection()
            .AddSingleton(AwJson.CreateOptions(SindriJsonContext.Default))
            .AddSingleton<IService<GetConnectionRequest, GetConnectionResponse>>(world.Get())
            .AddSingleton<IService<ListConnectionsRequest, ListConnectionsResponse>>(world.List())
            .BuildServiceProvider();
        return (services, world);
    }

    private static Dictionary<string, JsonElement> Arguments(string name)
        => new(StringComparer.Ordinal) { ["name"] = JsonSerializer.SerializeToElement(name) };

    private static string Text(ModelContextProtocol.Protocol.CallToolResult result)
        => (result.StructuredContent?.GetRawText() ?? string.Empty) + string.Concat(result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().Select(static t => t.Text));

    private static void AssertNoSecret(string text)
    {
        Assert.DoesNotContain("SECRET", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Access[..8], text, StringComparison.Ordinal);   // no token prefix either
        Assert.DoesNotContain("sindri-connection", text, StringComparison.Ordinal);
        Assert.DoesNotContain("keychain", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task connection_get_never_returns_a_secret_or_token_prefix()
    {
        var (services, _) = await Seeded();
        await using (services)
        {
            var arguments = Arguments("linear:work");

            var result = await ConnectionTools.CallGet(services, arguments, Token);

            Assert.True(result.IsError != true);
            var text = Text(result);
            Assert.Contains("linear:work", text, StringComparison.Ordinal);
            AssertNoSecret(text);
        }
    }

    [Fact]
    public async Task connection_list_never_returns_a_secret_or_token_prefix()
    {
        var (services, _) = await Seeded();
        await using (services)
        {
            var result = await ConnectionTools.CallList(services, new Dictionary<string, JsonElement>(StringComparer.Ordinal), Token);

            Assert.True(result.IsError != true);
            Assert.Contains("linear:work", Text(result), StringComparison.Ordinal);
            AssertNoSecret(Text(result));
        }
    }

    [Fact]
    public async Task connection_get_of_an_unknown_name_is_a_tool_error_with_the_code()
    {
        var (services, _) = await Seeded();
        await using (services)
        {
            var arguments = Arguments("linear:nobody");

            var result = await ConnectionTools.CallGet(services, arguments, Token);

            Assert.True(result.IsError);
            Assert.Contains("SND-CONN-001", Text(result), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_five_tools_are_named_and_agents_may_use_every_one()
    {
        Assert.Equal(["connection_list", "connection_get", "connection_add", "connection_test", "connection_remove"], ConnectionTools.All.Select(static t => t.Name), StringComparer.Ordinal);
        Assert.True(ConnectionTools.All.Single(static t => t.Name == "connection_get").Annotations!.ReadOnlyHint);
        Assert.True(ConnectionTools.All.Single(static t => t.Name == "connection_remove").Annotations!.DestructiveHint);
        Assert.All(ConnectionTools.All, static t => Assert.DoesNotContain("token", t.InputSchema.GetRawText(), StringComparison.OrdinalIgnoreCase));
    }
}
