namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>One running toolkit process with its MCP client attached.</summary>
internal interface IToolkitProcess : IAsyncDisposable
{
    /// <summary>Completes when the process ends or its connection drops, however that happened.</summary>
    public Task Exited { get; }

    /// <summary>Calls one tool.</summary>
    /// <param name="tool">The tool name (the manifest's, without the toolkit prefix).</param>
    /// <param name="inputJson">The arguments as a JSON object.</param>
    /// <param name="timeout">How long to wait for the answer.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer's text, SND-TK-006 on a timeout, SND-TK-007 for an unknown tool, or SND-TK-004 when the process ended.</returns>
    public ValueTask<Kernel.Result<string>> Call(string tool, string inputJson, TimeSpan timeout, CancellationToken cancellationToken);
}
