using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri memory list|create|search|write|forget' (agent-first PR 13): a client of the /sindri/memories routes. A session may run every verb.
/// <c>create &lt;name&gt; [--description T]</c>, <c>search &lt;name&gt; &lt;query&gt; [--level loose|balanced|strict] [--limit N]</c>, <c>write &lt;name&gt; &lt;text&gt; [--tag T]... [--expires-at D]</c>,
/// <c>forget &lt;name&gt; (--fact KEY | --tag T | --expired)</c>; every verb takes <c>--json</c>.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class MemoryVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    private readonly ControlPlaneClient _client = client;
    private readonly Output _output = output;
    private readonly Func<bool, CancellationToken, Task<Error?>>? _ensureRunning = ensureRunning;

    internal Task<int> Run(string[] args, CancellationToken ct) => throw Pending();

    // A red stub: the ports are held so the tests build the class the way the green one is built.
    private NotSupportedException Pending()
    {
        _ = (_client, _output, _ensureRunning);
        return new NotSupportedException();
    }
}
