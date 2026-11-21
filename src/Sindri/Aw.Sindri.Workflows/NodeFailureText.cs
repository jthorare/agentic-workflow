using System.Globalization;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The text of a loop item's failure and its split into the named text and the bare "code: message" (#3304). One place builds and splits the two shapes
/// <see cref="GraphIterationWorkflow"/> gives, so they cannot drift: "node 'bad' failed on item 1: why" and
/// "node 'bad' failed on item 1 and has onFailure continue, but it has no 'out' edge to continue on: why".
/// </summary>
internal static class NodeFailureText
{
    private const string Head = "node '";
    private const string Middle = "' failed on item ";
    private const string NoOut = " and has onFailure continue, but it has no 'out' edge to continue on";
    private const string Separator = ": ";

    /// <summary>The failure of a node that stops the item.</summary>
    /// <param name="node">The node id.</param>
    /// <param name="item">The item's position.</param>
    /// <param name="error">The node run's bare error.</param>
    /// <returns>The text the loop and the run carry.</returns>
    internal static string Stopped(string node, int item, string? error) => On(node, item) + Separator + error;

    /// <summary>The failure of a node that continues but has no 'out' edge to continue on.</summary>
    /// <param name="node">The node id.</param>
    /// <param name="item">The item's position.</param>
    /// <param name="error">The node run's bare error.</param>
    /// <returns>The text the loop and the run carry.</returns>
    internal static string Stranded(string node, int item, string? error) => On(node, item) + NoOut + Separator + error;

    /// <summary>Separates the prefix a loop item's failure carries from the error under it.</summary>
    /// <param name="failure">The loop's failure text.</param>
    /// <returns>The text to keep as it is for the run (null when the failure has no node prefix) and the text without the prefix for the node's row.</returns>
    internal static (string? Named, string Bare) Split(string failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var at = failure.StartsWith(Head, StringComparison.Ordinal) ? failure.IndexOf(Middle, Head.Length, StringComparison.Ordinal) : -1;
        if (at < 0)
        {
            return (null, failure);
        }

        var pos = at + Middle.Length;
        var digits = pos;
        while (digits < failure.Length && char.IsAsciiDigit(failure[digits]))
        {
            digits++;
        }

        if (digits == pos)
        {
            return (null, failure);
        }

        foreach (var tail in new[] { Separator, NoOut + Separator })
        {
            if (string.CompareOrdinal(failure, digits, tail, 0, tail.Length) == 0)
            {
                return (failure, failure[(digits + tail.Length)..]);
            }
        }

        return (null, failure);
    }

    private static string On(string node, int item) => Head + node + Middle + item.ToString(CultureInfo.InvariantCulture);
}
