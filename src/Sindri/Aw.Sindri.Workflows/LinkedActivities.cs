using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'LiveLinked' (LN-WF1): the linked instances of an instance that are still running, so its cancel can reach them. A linked instance
/// is a top-level workflow, not a Temporal child, so the parent finds them by the visibility query on the item they share with it.
/// </summary>
/// <param name="instances">The live instances, by a visibility query on 'Item'.</param>
internal sealed class LinkedActivities(IWorkflowInstances instances)
{
    /// <summary>The activity: the addresses of the running instances linked to <paramref name="run"/>'s instance.</summary>
    /// <param name="run">The ending instance and its item.</param>
    /// <returns>The linked instances' addresses.</returns>
    [Activity("LiveLinked")]
    public async Task<IReadOnlyList<string>> LiveLinkedAsync(ReleaseRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var live = await instances.List($"Item = '{run.Item.Replace("'", "''", StringComparison.Ordinal)}'", ended: false, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);
        return live.IsError
            ? throw new InvalidOperationException($"{live.Error.Code}: {live.Error.Message}")   // retried: the cancel must reach them
            : LinkedTo(InstanceAddress.From(run.Address), live.Value);
    }

    /// <summary>The rows whose instance is linked to <paramref name="parent"/> (its address is the parent's with a linked last segment).</summary>
    /// <param name="parent">The instance whose linked instances are wanted.</param>
    /// <param name="rows">The live instances of its item.</param>
    /// <returns>The linked instances' addresses.</returns>
    internal static IReadOnlyList<string> LinkedTo(InstanceAddress parent, IEnumerable<InstanceRow> rows) =>
        [.. rows.Where(row => InstanceAddress.TryFrom(row.Address, out var address) && address.LinkedParent() is { } linked && linked == parent).Select(static row => row.Address)];
}
