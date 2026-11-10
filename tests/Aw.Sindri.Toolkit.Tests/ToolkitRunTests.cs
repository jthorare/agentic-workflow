using System.IO.Pipes;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Toolkit.Tests;

[Collection(ProcessConsole.Name)]
public sealed class ToolkitRunTests
{
    private static readonly Dictionary<string, object?> _arguments = new(StringComparer.Ordinal) { ["left"] = "a", ["right"] = "b" };

    [Fact, Trait("Lane", "unit")]
    public async Task A_handler_that_writes_to_Console_Out_does_not_corrupt_the_MCP_stream()
    {
        var ct = TestContext.Current.CancellationToken;
        var toolkit = Toolkit.Named("chatty")
            .UseJson(SampleJson.Default)
            .Tool<DiffRequest, DiffResponse>("chat", "Prints to the console, then answers.", (request, _, _) =>
            {
                Console.Out.WriteLine("noise on the console");
                return Task.FromResult(new DiffResponse($"{request.Left} vs {request.Right}", 1));
            });
        using var toServer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var serverInput = new AnonymousPipeClientStream(PipeDirection.In, toServer.ClientSafePipeHandle);
        using var fromServer = new AnonymousPipeServerStream(PipeDirection.Out);
        using var clientInput = new AnonymousPipeClientStream(PipeDirection.In, fromServer.ClientSafePipeHandle);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var realOut = Console.Out;

        var serving = Task.Run(() => ToolkitHost.RunAsync(toolkit, [], new StandardStreams(() => serverInput, () => fromServer, stdout, stderr), ct), ct);
        var client = await McpClient.CreateAsync(new StreamClientTransport(toServer, clientInput), cancellationToken: ct);
        var result = await client.CallToolAsync("chat", _arguments, cancellationToken: ct);
        await client.DisposeAsync();
        await toServer.DisposeAsync();
        var exit = await serving;

        Assert.NotEqual(true, result.IsError);
        Assert.Equal(0, exit);
        Assert.Contains("noise on the console", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Same(realOut, Console.Out);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_manifest_flag_writes_toolkit_json_and_returns_0()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var toolkit = SampleToolkit.Create();

        var exit = await ToolkitHost.RunAsync(toolkit, ["--manifest"], new StandardStreams(() => throw new InvalidOperationException(), () => throw new InvalidOperationException(), stdout, stderr), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Equal(toolkit.Manifest(), stdout.ToString());
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_unknown_argument_prints_usage_and_returns_2()
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = await ToolkitHost.RunAsync(SampleToolkit.Create(), ["--nope"], new StandardStreams(() => throw new InvalidOperationException(), () => throw new InvalidOperationException(), stdout, stderr), TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.Contains("usage: contracts", stderr.ToString(), StringComparison.Ordinal);
    }
}
