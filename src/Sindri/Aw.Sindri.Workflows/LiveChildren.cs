using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Reads each child of a parent's status live (engine P7 plan, owner answer Q2): the parent keeps the children it started and the read asks each one now, so status never shows a stale child state.</summary>
internal static class LiveChildren
{
    /// <summary>What a child that couldn't be reached shows as its state.</summary>
    public const string Unreachable = "unreachable";

    /// <summary>The parent's view with every child's state as that child answers now.</summary>
    /// <param name="view">The parent's own view.</param>
    /// <param name="query">Asks one child for its view.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The view; the same one when it lists no child.</returns>
    public static async Task<InstanceView> Of(InstanceView view, Func<InstanceAddress, CancellationToken, Task<InstanceView>> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(query);
        if (view.Children.Count == 0)
        {
            return view;
        }

        var states = await Task.WhenAll(view.Children.Select(child => StateOf(child.Address, query, cancellationToken))).ConfigureAwait(false);
        return view with { Children = [.. view.Children.Zip(states, static (child, state) => child with { State = state })] };
    }

    private static async Task<string> StateOf(InstanceAddress address, Func<InstanceAddress, CancellationToken, Task<InstanceView>> query, CancellationToken cancellationToken)
    {
        try
        {
            return (await query(address, cancellationToken).ConfigureAwait(false)).State;
        }
        catch (Exception failed) when (failed is TemporalException or TimeoutException)
        {
            return Unreachable;   // boundary: a child nobody answers for is shown as such, never as its last known state
        }
    }
}
