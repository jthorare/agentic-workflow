using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Temporalio.Client;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// What every call on an instance's workflow shares (engine spec rev 8 §5.2, §5.3): an Update waits at most
/// <see cref="Patience"/> for a worker to take it, and a NotFound that names the namespace says the Temporal it reached
/// isn't serve's (SND-API-001), not that the instance is gone.
/// </summary>
internal static class TemporalUpdates
{
    /// <summary>
    /// How long an Update waits for a worker: 4 s, so a command that first waited the 5 s command gate still answers
    /// inside the CLI's 10 s HTTP timeout, and a serve without its worker fails cleanly instead of hanging.
    /// </summary>
    internal static readonly TimeSpan Patience = TimeSpan.FromSeconds(4);

    /// <summary>
    /// The Update's wait: one RPC bounded by <see cref="Patience"/> in total. The per-RPC timeout alone is not a bound: the
    /// client core retries an RPC that timed out, so a serve without a worker took 8 s and sometimes 12 s to answer, past the
    /// CLI's 10 s HTTP timeout. The linked token is cancelled after <see cref="Patience"/>, whatever the retries did.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait (the caller's).</param>
    /// <returns>The options to send with the Update; dispose it when the call is over.</returns>
    internal static UpdateWait Wait(CancellationToken cancellationToken) => new(cancellationToken);

    /// <summary>The Update's RPC options with their total bound; see <see cref="Wait"/>.</summary>
    internal sealed class UpdateWait : IDisposable
    {
        private readonly CancellationTokenSource _patience;

        internal UpdateWait(CancellationToken cancellationToken)
        {
            _patience = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _patience.CancelAfter(Patience);
            Options = new RpcOptions { CancellationToken = _patience.Token, Timeout = Patience };
        }

        /// <summary>The RPC options: the caller's token plus <see cref="Patience"/> in total, and <see cref="Patience"/> for one attempt.</summary>
        internal RpcOptions Options { get; }

        public void Dispose() => _patience.Dispose();
    }

    /// <summary>Whether <paramref name="e"/> is the frontend saying it has no such namespace.</summary>
    /// <param name="e">The RPC failure.</param>
    /// <returns>True for a NotFound that names the namespace as the missing thing, not a workflow execution inside it.</returns>
    internal static bool NoNamespace(RpcException e) =>
        e.Code == RpcException.StatusCode.NotFound && e.Message.Contains("namespace", StringComparison.OrdinalIgnoreCase)
        && !e.Message.Contains("execution not found", StringComparison.OrdinalIgnoreCase);   // a missing workflow's message names its namespace too ("WorkflowId{namespace='default', …}")

    /// <summary>SND-API-001 for a Temporal without the client's namespace.</summary>
    /// <param name="client">The client, on the settings' namespace.</param>
    /// <param name="e">The RPC failure.</param>
    /// <returns>The error.</returns>
    internal static Error NotServes(ITemporalClient client, RpcException e) =>
        SindriErrors.NotRunning.With($"Temporal at {client.Connection.Options.TargetHost} has no '{client.Options.Namespace}' namespace: {e.Message} "
            + "(fix: start sindri serve on this home, or set AW_SINDRI_TEMPORAL_NAMESPACE to the namespace serve uses)");

    /// <summary>
    /// SND-API-001 for a frontend that went away under a live client (the dev server under serve died; serve itself answered):
    /// Unavailable is the transport saying so. A call that carries a nonce says what to check and how to resend it, because the
    /// Update may have reached the server before it went; a start or a signal has no nonce.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="e">The RPC failure.</param>
    /// <param name="nonce">The Update's id when the call has one: a retry with it resends the same Update id.</param>
    /// <returns>The error; its kind (Unavailable) keeps a fresh nonce claim.</returns>
    internal static Error Down(ITemporalClient client, RpcException e, CommandNonce? nonce = null) => Down(client, e.Message, nonce);

    /// <summary><see cref="Down(ITemporalClient, RpcException, CommandNonce?)"/> with a reason that is not an RPC failure.</summary>
    /// <param name="client">The client.</param>
    /// <param name="reason">What the probe found.</param>
    /// <param name="nonce">The Update's id when the call has one.</param>
    /// <returns>The error.</returns>
    internal static Error Down(ITemporalClient client, string reason, CommandNonce? nonce) => Down(client.Connection.Options.TargetHost, reason, nonce);

    /// <summary><see cref="Down(ITemporalClient, string, CommandNonce?)"/> for a frontend known by its address.</summary>
    /// <param name="target">The frontend's host:port.</param>
    /// <param name="reason">What the probe found.</param>
    /// <param name="nonce">The Update's id when the call has one.</param>
    /// <returns>The error.</returns>
    internal static Error Down(string? target, string reason, CommandNonce? nonce) =>
        SindriErrors.NotRunning.With($"Temporal at {target} doesn't answer: {reason} ("
            + (nonce is { } n
                ? $"fix: check `sindri workflow status` or `sindri workflow events` first; if the command didn't apply, run the same command again with --nonce {n.Value}; "
                : "fix: ")
            + "the Temporal dev server under serve is down, see `sindri doctor`)");

    /// <summary>
    /// What an Update that ran out of <see cref="Patience"/> means: a Temporal that doesn't answer a health check (the dev
    /// server under serve died: the client core was still retrying Unavailable) is SND-API-001, else no worker took it
    /// (<see cref="NoWorker(InstanceAddress, CommandNonce)"/>).
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The Update's id.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The error.</returns>
    internal static ValueTask<Error> NoAnswer(ITemporalClient client, InstanceAddress address, CommandNonce nonce, CancellationToken cancellationToken) =>
        NoAnswer(client.Connection.Options.TargetHost, options => client.Connection.CheckHealthAsync(options: options), address, nonce, cancellationToken);

    /// <summary><see cref="NoAnswer(ITemporalClient, InstanceAddress, CommandNonce, CancellationToken)"/> over a health probe (the test seam).</summary>
    /// <param name="target">The frontend's host:port.</param>
    /// <param name="healthy">The health check, run with its 2 s RPC options.</param>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The Update's id.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The error.</returns>
    internal static async ValueTask<Error> NoAnswer(string? target, Func<RpcOptions, Task<bool>> healthy, InstanceAddress address, CommandNonce nonce, CancellationToken cancellationToken)
    {
        try
        {
            return await healthy(new RpcOptions { Timeout = TimeSpan.FromSeconds(2), CancellationToken = cancellationToken }).ConfigureAwait(false)
                ? NoWorker(address, nonce)
                : Down(target, "its health check failed", nonce);
        }
        catch (RpcException e) // boundary: the probe says the frontend is gone
        {
            return Down(target, e.Message, nonce);
        }
    }

    /// <summary><see cref="NoAnswer(ITemporalClient, InstanceAddress, CommandNonce, CancellationToken)"/> for an Update id that may be the engine's own ('wf:{instance}:open:{launch}') and no nonce.</summary>
    /// <param name="client">The client.</param>
    /// <param name="address">The instance.</param>
    /// <param name="updateId">The Update's id.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The error.</returns>
    internal static ValueTask<Error> NoAnswer(ITemporalClient client, InstanceAddress address, string updateId, CancellationToken cancellationToken) =>
        CommandNonce.TryFrom(updateId, out var nonce)
            ? NoAnswer(client, address, nonce, cancellationToken)
            : NoAnswerEngine(client, address, updateId, cancellationToken);

    private static async ValueTask<Error> NoAnswerEngine(ITemporalClient client, InstanceAddress address, string updateId, CancellationToken cancellationToken)
    {
        try
        {
            return await client.Connection.CheckHealthAsync(options: new RpcOptions { Timeout = TimeSpan.FromSeconds(2), CancellationToken = cancellationToken }).ConfigureAwait(false)
                ? NoWorker(address, updateId)
                : Down(client, "its health check failed", nonce: null);
        }
        catch (RpcException e) // boundary: the probe says the frontend is gone
        {
            return Down(client, e);
        }
    }

    /// <summary>SND-API-002 for an Update no worker took in <see cref="Patience"/>; it may still apply once one runs.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="nonce">The Update's id, for the retry.</param>
    /// <returns>The error.</returns>
    internal static Error NoWorker(InstanceAddress address, CommandNonce nonce) =>
        SindriErrors.ApiTimeout.With($"no worker took the command for {address.Value} within {Patience.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s; it may still apply once one runs "
            + $"(fix: start sindri serve, which runs the worker, then run the same command again with --nonce {nonce.Value})");

    /// <summary>SND-API-002 for the engine's own Update (an 'open-request') no worker took in <see cref="Patience"/>.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="updateId">The Update's id ('wf:{instance}:open:{launch}'): a repeat with it applies once.</param>
    /// <returns>The error.</returns>
    internal static Error NoWorker(InstanceAddress address, string updateId) =>
        SindriErrors.ApiTimeout.With($"no worker took {updateId} for {address.Value} within {Patience.TotalSeconds.ToString("0", CultureInfo.InvariantCulture)} s; it may still apply once one runs "
            + "(fix: start sindri serve, which runs the worker; sending it again applies it once)");
}
