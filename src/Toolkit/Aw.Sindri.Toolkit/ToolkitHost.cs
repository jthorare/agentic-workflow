using ModelContextProtocol.Server;

namespace Aw.Sindri.Toolkit;

/// <summary>Runs a toolkit as an MCP stdio server.</summary>
public static class ToolkitHost
{
    /// <summary>The entry point: "--manifest" prints toolkit.json and returns 0; no argument serves MCP on stdin and stdout until stdin closes.</summary>
    /// <param name="toolkit">The declaration.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>The exit code: 0 ok, 2 usage.</returns>
    public static async Task<int> RunAsync(Toolkit toolkit, string[] args, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 1 && string.Equals(args[0], "--manifest", StringComparison.Ordinal))
        {
            await Console.Out.WriteAsync(toolkit.Manifest()).ConfigureAwait(false);
            return 0;
        }

        if (args.Length != 0)
        {
            await Console.Error.WriteLineAsync($"usage: {toolkit.Name} [--manifest]  (no argument serves MCP on stdin and stdout)").ConfigureAwait(false);
            return 2;
        }

        var input = Console.OpenStandardInput();
        await using (input.ConfigureAwait(false))
        {
            var output = Console.OpenStandardOutput();
            await using (output.ConfigureAwait(false))
            {
                await ServeAsync(toolkit, input, output, Environment.GetEnvironmentVariable, cancellationToken).ConfigureAwait(false);
            }
        }

        return 0;
    }

    /// <summary>Serves MCP over two streams until the input closes. Tests use this with an in-memory pair.</summary>
    /// <param name="toolkit">The declaration.</param>
    /// <param name="input">Requests from the client.</param>
    /// <param name="output">Responses to the client.</param>
    /// <param name="environment">Reads one environment variable; null when it is not set.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>A task that ends when the session ends.</returns>
    public static async Task ServeAsync(Toolkit toolkit, Stream input, Stream output, Func<string, string?> environment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(environment);
        var options = new ToolkitServer(toolkit, environment).Options();
        var transport = new StreamServerTransport(input, output, toolkit.Name);
        await using (transport.ConfigureAwait(false))
        {
            var server = McpServer.Create(transport, options);
            await using (server.ConfigureAwait(false))
            {
                await server.RunAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
