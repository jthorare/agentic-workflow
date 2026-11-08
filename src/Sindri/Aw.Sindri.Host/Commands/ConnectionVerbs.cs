using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri connection add|list|show|test|remove [--client-id ID] [--key-env NAME] [--kind K] [--preset P]' (connections PR 10a): a client of the /sindri/connections routes.
/// A session may run every verb (owner decision D2). 'add' for an oauth connection prints the consent URL as soon as it has it, then waits for the callback.
/// </summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ConnectionVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
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
