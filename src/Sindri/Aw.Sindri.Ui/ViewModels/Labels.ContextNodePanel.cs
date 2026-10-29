namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the whole map's node side panel.</summary>
internal static partial class Labels
{
    /// <summary>The panel's accessible name.</summary>
    internal const string ContextNodePanelName = "Item details";

    /// <summary>The close button's words.</summary>
    internal const string ContextNodePanelClose = "Close (Esc)";

    /// <summary>The line while the node is read.</summary>
    internal const string ContextNodePanelLoading = "Reading this item…";

    /// <summary>The line when the read failed.</summary>
    internal const string ContextNodePanelFailed = "Couldn't read this item";

    /// <summary>The heading over the links.</summary>
    internal const string ContextNodePanelLinks = "Linked to";

    /// <summary>Opens the source's link.</summary>
    internal const string ContextNodePanelOpenSource = "Open source ↗";

    /// <summary>The muted line when the node has a source but no link to open (#1702).</summary>
    internal const string ContextNodePanelNoLink = "No link to the source";

    /// <summary>The open-source button's accessible name: what it opens, by the source's kind (#1702).</summary>
    /// <param name="kind">The source kind.</param>
    /// <returns>"Open the ticket in the browser".</returns>
    internal static string ContextNodePanelOpenSourceName(string kind) => kind switch
    {
        "ticket" => "Open the ticket in the browser",
        "doc" or "file" => "Open the file on GitHub in the browser",
        _ => "Open the source in the browser",
    };

    /// <summary>One link row: the other node, the way it links and which way.</summary>
    /// <param name="label">The other node's label (or key).</param>
    /// <param name="type">The edge type.</param>
    /// <param name="outgoing">True when the edge leaves the node.</param>
    /// <returns>"→ Tab manager · touches".</returns>
    internal static string ContextNodePanelEdge(string label, string type, bool outgoing) => (outgoing ? "→ " : "← ") + label + " · " + type;

    /// <summary>The source line: its kind and title or reference.</summary>
    /// <param name="kind">The source kind.</param>
    /// <param name="text">The title, else the reference.</param>
    /// <returns>"ticket: LIN-12".</returns>
    internal static string ContextNodePanelSource(string kind, string text) => kind + ": " + text;

    /// <summary>The caption on a cited record that has no link to open.</summary>
    internal const string ContextNoLink = "no link";

    /// <summary>The place a record's source word names; null for a source with no name of its own.</summary>
    /// <param name="source">The record's source ("linear", "linear:doc", "github", "slack", ...).</param>
    /// <returns>"Linear", "GitHub" or "Slack", else null.</returns>
    internal static string? ContextSourcePlace(string source) => source switch
    {
        "linear" or "linear:doc" => "Linear",
        "github" => "GitHub",
        "slack" => "Slack",
        _ => null,
    };

    /// <summary>The button that opens a record at its source.</summary>
    /// <param name="source">The record's source.</param>
    /// <returns>"Open in Linear", else "Open source".</returns>
    internal static string ContextOpenIn(string source) => ContextSourcePlace(source) is { } place ? "Open in " + place : "Open source";

    /// <summary>The same button's name for a screen reader.</summary>
    /// <param name="name">The record's identifier, else its label.</param>
    /// <param name="source">The record's source.</param>
    /// <returns>"Open SPL-103 in Linear".</returns>
    internal static string ContextOpenInName(string name, string source) => ContextSourcePlace(source) is { } place ? "Open " + name + " in " + place : "Open " + name + " source";
}
