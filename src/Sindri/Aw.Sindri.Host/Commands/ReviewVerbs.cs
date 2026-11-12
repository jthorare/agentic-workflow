using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri reviews', 'sindri review show' and 'sindri review fix' (PR review page spec §6, §8.2): clients of the review routes.</summary>
/// <param name="client">The API client.</param>
/// <param name="output">Where the verbs write.</param>
/// <param name="ensureRunning">Starts the control plane (true: this verb may launch it) or names why not; null: never start it.</param>
internal sealed class ReviewVerbs(ControlPlaneClient client, Output output, Func<bool, CancellationToken, Task<Error?>>? ensureRunning = null)
{
    private readonly ControlPlaneClient _client = client;
    private readonly Output _output = output;
    private readonly Func<bool, CancellationToken, Task<Error?>>? _ensureRunning = ensureRunning;

    internal Task<int> Run(string[] args, CancellationToken ct) => throw new NotSupportedException($"review verbs are not built yet ({_client is null}, {_output is null}, {_ensureRunning is null})");
}
