namespace Aw.Sindri.Toolkit;

/// <summary>Runs a toolkit as an MCP stdio server.</summary>
public static class ToolkitHost
{
    /// <summary>The entry point: "--manifest" prints toolkit.json and returns 0; anything else serves MCP on stdin and stdout until stdin closes.</summary>
    /// <param name="toolkit">The declaration.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>The exit code: 0 ok, 2 usage.</returns>
    public static Task<int> RunAsync(Toolkit toolkit, string[] args, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <summary>Serves MCP over two streams until the input closes. Tests use this with an in-memory pair.</summary>
    /// <param name="toolkit">The declaration.</param>
    /// <param name="input">Requests from the client.</param>
    /// <param name="output">Responses to the client.</param>
    /// <param name="environment">Reads one environment variable; null when it is not set.</param>
    /// <param name="cancellationToken">Stops the server.</param>
    /// <returns>A task that ends when the session ends.</returns>
    public static Task ServeAsync(Toolkit toolkit, Stream input, Stream output, Func<string, string?> environment, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
