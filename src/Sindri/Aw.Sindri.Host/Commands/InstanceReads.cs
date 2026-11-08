using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// What the read verbs share (engine spec §6.3; review run 2, architecture-strategist-r2-4): they are clients of the
/// control plane's instance routes, like the write verbs, and a read never starts it. Only 'recipe validate' and
/// 'recipe eval' run in process.
/// </summary>
internal static class InstanceReads
{
    /// <summary>The route the instances are under.</summary>
    internal const string Instances = "/sindri/workflow/instances";

    /// <summary>
    /// The opaque id of the instance <paramref name="reference"/> names (an address or a bare item), through
    /// 'GET /workflow/instances?address=', the only place an address travels. Does not start the control plane, and returns the error as it came
    /// (a read says 'not running' with <see cref="NotRunning"/>; a write has already ensured it runs).
    /// </summary>
    /// <param name="desk">The API client.</param>
    /// <param name="reference">The address or bare item.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The id, SND-WF-001 for no such instance, or SND-API-001 when serve isn't running.</returns>
    internal static async Task<Result<WorkflowInstanceId>> IdOf(RecipeDesk desk, string reference, CancellationToken ct)
    {
        var listed = await desk.Client.Get<InstanceList>($"{Instances}?address={Uri.EscapeDataString(reference)}", ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        return listed.Value.Value.Instances is [var instance]
            ? instance.Id
            : WorkflowErrors.InstanceNotFound.With($"'{reference}' names no one instance (fix: sindri workflow list)");
    }

    /// <summary>
    /// The id for reading an instance's trail: <see cref="IdOf"/>, except that an address Temporal no longer knows (retention,
    /// SND-WF-001) or can't answer for (no worker, SND-API-002) still names its ledger rows, whose reader needs no Temporal.
    /// </summary>
    /// <param name="desk">The API client.</param>
    /// <param name="reference">The address or bare item.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The id, or the error when the reference can't name an instance without Temporal.</returns>
    internal static async Task<Result<WorkflowInstanceId>> IdForTrail(RecipeDesk desk, string reference, CancellationToken ct)
    {
        var resolved = await IdOf(desk, reference, ct).ConfigureAwait(false);
        return resolved.IsError && InstanceAddress.TryFrom(reference, out var typed)
            && (resolved.Error.Code == WorkflowErrors.InstanceNotFound.Code || resolved.Error.Code == SindriErrors.ApiTimeout.Code)
            ? WorkflowInstanceId.Of(typed)
            : resolved.IsError ? NotRunning(resolved.Error) : resolved;
    }

    /// <summary>A query never starts the control plane; when it is down the error says so.</summary>
    /// <param name="error">The error a call returned.</param>
    /// <returns>The error, prefixed 'not running: ' when it is SND-API-001.</returns>
    internal static Error NotRunning(Error error) =>
        error.Code == SindriErrors.NotRunning.Code ? error with { Message = "not running: " + error.Message } : error;
}
