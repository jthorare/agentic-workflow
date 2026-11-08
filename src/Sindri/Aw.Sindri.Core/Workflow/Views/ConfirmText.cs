using System.Globalization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The confirmation sentences of destructive verbs (owner decision 2026-10-14; API spec section 3.8 'commands[].confirm').</summary>
public static class ConfirmText
{
    /// <summary>The sentence for cancelling an instance: what stops and what is cancelled with it. Singular at 1 (provisional owner default, 2026-10-14 night).</summary>
    /// <param name="address">The instance's address.</param>
    /// <param name="runningSteps">How many steps are running now (the open run).</param>
    /// <param name="openChildren">How many child instances are still open.</param>
    /// <returns>The sentence.</returns>
    public static string Cancel(InstanceAddress address, int runningSteps, int openChildren)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(runningSteps);
        ArgumentOutOfRangeException.ThrowIfNegative(openChildren);
        var steps = runningSteps == 1 ? "running step stops" : "running steps stop";
        var children = openChildren == 1 ? "child is" : "children are";
        return string.Create(CultureInfo.InvariantCulture, $"Cancel {address.Value}? {runningSteps} {steps}; {openChildren} {children} cancelled.");
    }
}
