using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>A stdio MCP client (ModelContextProtocol.Core) on one toolkit process: it spawns the command it is given, which is the sandbox's wrapped argv, and ends when that process ends.</summary>
internal sealed class ToolkitMcpClient : IToolkitProcess
{
    /// <summary>How long a call waits when the caller gives no timeout.</summary>
    internal static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromSeconds(60);

    private readonly McpClient _client;

    private ToolkitMcpClient(McpClient client)
    {
        _client = client;
    }

    public Task Exited => _client.Completion;

    /// <summary>Starts the command and connects to it.</summary>
    /// <param name="name">The toolkit name, for the client's own labels.</param>
    /// <param name="argv">The command and its arguments (already wrapped by the sandbox).</param>
    /// <param name="workingDirectory">The process's folder.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The connected client, or SND-TK-004 when the process ended or didn't speak MCP.</returns>
    internal static async ValueTask<Result<ToolkitMcpClient>> Connect(string name, IReadOnlyList<string> argv, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(argv);
        try
        {
#pragma warning disable CA2000 // the client owns the transport and is disposed with it
            var transport = new StdioClientTransport(new StdioClientTransportOptions { Name = name, Command = argv[0], Arguments = [.. argv.Skip(1)], WorkingDirectory = workingDirectory });
#pragma warning restore CA2000
            var options = new McpClientOptions { ClientInfo = new Implementation { Name = "sindri", Version = "1" } };
            var client = await McpClient.CreateAsync(transport, options, loggerFactory: null, cancellationToken).ConfigureAwait(false);
            return new ToolkitMcpClient(client);
        }
        catch (Exception e) when (e is McpException or IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return SindriErrors.ToolkitProcessDied.With($"{name} didn't start as an MCP server: {e.Message}");
        }
    }

    public async ValueTask<Result<string>> Call(string tool, string inputJson, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var limited = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limited.CancelAfter(timeout);
        var arguments = Arguments(inputJson);
        if (arguments.IsError)
        {
            return arguments.Error;
        }

        try
        {
            var answered = await _client.CallToolAsync(tool, arguments.Value, progress: null, options: null, limited.Token).ConfigureAwait(false);
            var text = answered.StructuredContent is { } structured ? structured.GetRawText() : Text(answered);
            return answered.IsError == true ? SindriErrors.ToolkitToolFailed.With(text) : text;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.ToolkitCallTimedOut.With($"{tool} did not answer in {timeout.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture)} s");
        }
        catch (McpException e) when (e.Message.Contains("unknown tool", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return SindriErrors.ToolkitToolUnknown.With(tool);
        }
        catch (Exception e) when (e is McpException or IOException or ObjectDisposedException)
        {
            return SindriErrors.ToolkitProcessDied.With(e.Message);
        }
    }

    /// <summary>Says which SND-TK error an MCP failure on a call is.</summary>
    /// <param name="tool">The tool called.</param>
    /// <param name="failure">What the client threw.</param>
    /// <returns>SND-TK-007 for an unknown tool, SND-TK-011 when the server answered with an error, SND-TK-004 when the connection failed.</returns>
    internal static Result<string> Failure(string tool, McpException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        _ = tool;
        return SindriErrors.ToolkitProcessDied.With(failure.Message);
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();

    private static Result<Dictionary<string, object?>> Arguments(string inputJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return SindriErrors.ToolkitManifestInvalid.With("a tool call's arguments are a JSON object");
            }

            return doc.RootElement.EnumerateObject().ToDictionary(static p => p.Name, static p => (object?)p.Value.Clone(), StringComparer.Ordinal);
        }
        catch (JsonException e)
        {
            return SindriErrors.ToolkitManifestInvalid.With("the arguments aren't JSON: " + e.Message);
        }
    }

    private static string Text(CallToolResult answered)
    {
        var text = new StringBuilder();
        foreach (var block in answered.Content.OfType<TextContentBlock>())
        {
            _ = text.Append(block.Text);
        }

        return text.ToString();
    }
}
