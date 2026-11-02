using Aw.UiDriver.Core.Protocol;

namespace Aw.UiDriver.Core.Selectors;

#pragma warning disable MA0182 // used by the Avalonia adapter and the CLI, which the analyzer can't see from here

/// <summary>Reads the wait states by name and decides whether one holds.</summary>
internal static class WaitStates
{
    /// <summary>Reads a state's name ('visible', 'textEquals'); null means the default, visible.</summary>
    /// <param name="name">The name.</param>
    /// <param name="state">The state.</param>
    /// <returns>False for a name that is none.</returns>
    internal static bool TryParse(string? name, out WaitState state)
    {
        state = WaitState.Visible;
        return name is null || (Enum.TryParse(name, ignoreCase: true, out state) && Enum.IsDefined(state));
    }

    /// <summary>Whether the state holds for the node the selector found now.</summary>
    /// <param name="state">The state.</param>
    /// <param name="node">The element, or null when the selector matches nothing now.</param>
    /// <param name="text">The text a text state compares with.</param>
    /// <returns>Whether it holds.</returns>
    internal static bool Holds(WaitState state, NodeFacts? node, string? text)
    {
        if (state == WaitState.Gone)
        {
            return node is null;
        }

        if (node is null)
        {
            return false;
        }

        return state switch
        {
            WaitState.Exists => true,
            WaitState.Gone => false,
            WaitState.NotVisible => false,   // one node cannot say it: the overload over every match decides it
            WaitState.Visible => node.Visible,
            WaitState.Hidden => !node.Visible,
            WaitState.Enabled => node.Enabled,
            WaitState.Disabled => !node.Enabled,
            WaitState.TextEquals => string.Equals(node.Text, text, StringComparison.Ordinal),
            WaitState.TextContains => text is not null && (node.Text?.Contains(text, StringComparison.Ordinal) ?? false),
            _ => false,
        };
    }

    /// <summary>Whether the state holds, for the states that look at every match (notVisible) and, for the rest, at the picked node.</summary>
    /// <param name="state">The state.</param>
    /// <param name="matches">Every element the selector matches now.</param>
    /// <param name="picked">The one the index picks, or null.</param>
    /// <param name="text">The text a text state compares with.</param>
    /// <returns>Whether it holds.</returns>
    internal static bool Holds(WaitState state, IReadOnlyList<NodeFacts> matches, NodeFacts? picked, string? text) =>
        state == WaitState.NotVisible ? matches.All(static m => !m.Visible) : Holds(state, picked, text);

    /// <summary>What a timed-out wait adds to its error: for gone, that a match still exists but is hidden.</summary>
    /// <param name="state">The state.</param>
    /// <param name="matches">Every element the selector matches now.</param>
    /// <returns>The hint, with a leading space, or empty.</returns>
    internal static string Hint(WaitState state, IReadOnlyList<NodeFacts> matches) =>
        state == WaitState.Gone && matches.Count > 0 && matches.All(static m => !m.Visible)
            ? " (a match is still in the tree but hidden; use notVisible to wait for removed or hidden)"
            : string.Empty;

    /// <summary>True for a state that needs the request's text.</summary>
    /// <param name="state">The state.</param>
    /// <returns>Whether it compares text.</returns>
    internal static bool NeedsText(WaitState state) => state is WaitState.TextEquals or WaitState.TextContains;
}
