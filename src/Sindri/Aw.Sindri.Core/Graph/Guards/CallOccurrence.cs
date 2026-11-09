namespace Aw.Sindri.Core.Graph.Guards;

/// <summary>The occurrence number of a run that another workflow started: made from the caller run, node and index, so calling twice gives one run.</summary>
public static class CallOccurrence
{
    /// <summary>The occurrence of the callee's run.</summary>
    /// <param name="caller">The calling run's id.</param>
    /// <param name="node">The calling node's id.</param>
    /// <param name="index">The node's item index, 0 outside a 'for_each'.</param>
    /// <returns>A number from 100,000,000 to 999,999,999: above the numbers a trigger counts up to, so a call never takes a manual run's id.</returns>
    public static int Of(string caller, string node, int index) => throw new NotSupportedException("CallOccurrence.Of is not written yet");
}
