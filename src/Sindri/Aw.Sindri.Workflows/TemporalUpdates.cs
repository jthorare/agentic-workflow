using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
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

    /// <summary>
    /// The one catch ladder every Update on an instance's workflow shares (arch-r4-3): the validator's refusal comes back as the
    /// catalogue error its ApplicationFailure names, a Temporal without the namespace is SND-API-001, an Update no worker took in
    /// <see cref="Patience"/> is decided by <see cref="NoAnswer(string?, Func{RpcOptions, Task{bool}}, InstanceAddress, CommandNonce, CancellationToken)"/>,
    /// no running execution is SND-WF-001, and a frontend that went away is SND-API-001 with the nonce to resend.
    /// </summary>
    /// <param name="frontend">The Temporal the Update goes to.</param>
    /// <param name="address">The instance.</param>
    /// <param name="updateId">The Update's id (a nonce, or the engine's own).</param>
    /// <param name="send">Sends the Update with the RPC options it is given.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The outcome, or the error the failure maps to.</returns>
    internal static async ValueTask<Result<CommandResult>> Execute(UpdateFrontend frontend, InstanceAddress address, string updateId, Func<RpcOptions, Task<CommandResult>> send, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(frontend);
        ArgumentNullException.ThrowIfNull(send);
        using var patience = Wait(cancellationToken);
        try
        {
            return await send(patience.Options).ConfigureAwait(false);
        }
        catch (WorkflowUpdateFailedException e) when (e.InnerException is ApplicationFailureException { ErrorType: { } code } refusal) // boundary: the validator's refusal is the caller's answer
        {
            return (WorkflowErrors.All.FirstOrDefault(d => d.Code == code) ?? WorkflowErrors.TriggerNotAllowed).With(refusal.Message);
        }
        catch (RpcException e) when (NoNamespace(e)) // boundary: not serve's Temporal; says nothing about the instance
        {
            return NotServes(frontend, e);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested && e is WorkflowUpdateRpcTimeoutOrCanceledException or RpcException { Code: RpcException.StatusCode.DeadlineExceeded }) // boundary: no worker took it
        {
            return await NoAnswer(frontend, address, updateId, cancellationToken).ConfigureAwait(false);
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: no running execution with that id
        {
            return WorkflowErrors.InstanceNotFound.With($"no running instance {address.Value} (fix: sindri workflow list)");
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.Unavailable) // boundary: the dev server died under a live serve
        {
            return Down(frontend.Host, e.Message, CommandNonce.TryFrom(updateId, out var nonce) ? nonce : null);
        }
    }

    /// <summary>What the ladder needs to know of the Temporal an Update goes to.</summary>
    /// <param name="Host">The frontend's host:port.</param>
    /// <param name="Namespace">The namespace the client is on.</param>
    /// <param name="Healthy">The health check, run with its 2 s RPC options.</param>
    internal sealed record UpdateFrontend(string? Host, string Namespace, Func<RpcOptions, Task<bool>> Healthy)
    {
        /// <summary>The frontend <paramref name="client"/> is connected to.</summary>
        /// <param name="client">The client.</param>
        /// <returns>Its facts.</returns>
        internal static UpdateFrontend Of(ITemporalClient client) =>
            new(client.Connection.Options.TargetHost, client.Options.Namespace, options => client.Connection.CheckHealthAsync(options: options));
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
    internal static Error NotServes(ITemporalClient client, RpcException e) => NotServes(UpdateFrontend.Of(client), e);

    /// <summary><see cref="NotServes(ITemporalClient, RpcException)"/> for a frontend known by its facts.</summary>
    /// <param name="frontend">The frontend.</param>
    /// <param name="e">The RPC failure.</param>
    /// <returns>The error.</returns>
    internal static Error NotServes(UpdateFrontend frontend, RpcException e) =>
        SindriErrors.NotRunning.With($"Temporal at {frontend.Host} has no '{frontend.Namespace}' namespace: {e.Message} "
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
        NoAnswer(UpdateFrontend.Of(client), address, updateId, cancellationToken);

    /// <summary><see cref="NoAnswer(ITemporalClient, InstanceAddress, string, CancellationToken)"/> over a frontend's facts.</summary>
    /// <param name="frontend">The frontend.</param>
    /// <param name="address">The instance.</param>
    /// <param name="updateId">The Update's id.</param>
    /// <param name="cancellationToken">The caller's token.</param>
    /// <returns>The error.</returns>
    internal static ValueTask<Error> NoAnswer(UpdateFrontend frontend, InstanceAddress address, string updateId, CancellationToken cancellationToken) =>
        CommandNonce.TryFrom(updateId, out var nonce)
            ? NoAnswer(frontend.Host, frontend.Healthy, address, nonce, cancellationToken)
            : NoAnswerEngine(frontend, address, updateId, cancellationToken);

    private static async ValueTask<Error> NoAnswerEngine(UpdateFrontend frontend, InstanceAddress address, string updateId, CancellationToken cancellationToken)
    {
        try
        {
            return await frontend.Healthy(new RpcOptions { Timeout = TimeSpan.FromSeconds(2), CancellationToken = cancellationToken }).ConfigureAwait(false)
                ? NoWorker(address, updateId)
                : Down(frontend.Host, "its health check failed", nonce: null);
        }
        catch (RpcException e) // boundary: the probe says the frontend is gone
        {
            return Down(frontend.Host, e.Message, nonce: null);
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
