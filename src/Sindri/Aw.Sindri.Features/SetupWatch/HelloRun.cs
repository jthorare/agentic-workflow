using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The one run of hello-sindri the setup service starts. It is scoped (the runner and the store are); whether the run is settled (started, refused as done, gone, or not ours) lives in the singleton state.
/// </summary>
/// <param name="runner">Starts the run (the command, run as 'service:setup').</param>
/// <param name="workflows">Reads the workflows, to tell the one setup made from one the owner made.</param>
/// <param name="state">What is remembered between ticks.</param>
internal sealed class HelloRun(ISetupWorkflowRunner runner, IWorkflowStore workflows, HelloRunState state)
{
    private const string SetupActor = "service:setup";

    /// <summary>Gets a value indicating whether nothing more is to be tried: the run started, was refused as done, hello-sindri is gone, or it is not ours.</summary>
    /// <returns>True when settled.</returns>
    internal bool IsSettled() => state.Settled;

    /// <summary>Records that nothing more is to be tried.</summary>
    internal void Settle() => state.Settled = true;

    /// <summary>Tells who made the hello-sindri in the store.</summary>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Ours when 'service:setup' made it, not ours when it is missing or someone else made it, unknown when the store could not answer.</returns>
    internal async ValueTask<HelloOwner> OwnerOf(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        var listed = await workflows.List(includeArchived: true, cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return HelloOwner.Unknown;
        }

        return listed.Value.FirstOrDefault(w => w.Slug == slug) is { } record && string.Equals(record.CreatedBy, SetupActor, StringComparison.Ordinal)
            ? HelloOwner.Ours
            : HelloOwner.NotOurs;
    }

    /// <summary>Starts the run when hello-sindri is ours; a start that failed is tried again on the next tick.</summary>
    /// <param name="slug">The workflow.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that ends when the start was tried.</returns>
    internal async ValueTask Once(WorkflowSlug slug, CancellationToken cancellationToken)
    {
        if (state.Settled)
        {
            return;
        }

        switch (await OwnerOf(slug, cancellationToken).ConfigureAwait(false))
        {
            case HelloOwner.Unknown:
                return;   // can't tell: asked again on the next tick, never taken for "not ours"
            case HelloOwner.NotOurs:
                Settle();
                return;
            case HelloOwner.Ours:
                break;
        }

        var started = await runner.Run(slug, cancellationToken).ConfigureAwait(false);
        state.Settled = started.IsSuccess || IsFinal(started.Error.Code);
    }

    // Already ran (132), or deleted or archived since: the run can never start, so stop asking.
    private static bool IsFinal(string code) =>
        string.Equals(code, SindriErrors.GraphSetupRunRefused.Code, StringComparison.Ordinal)
        || string.Equals(code, SindriErrors.GraphNotFound.Code, StringComparison.Ordinal)
        || string.Equals(code, SindriErrors.GraphArchived.Code, StringComparison.Ordinal);
}
