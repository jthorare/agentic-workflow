using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context map's inspector (F3 spec 1 §2 "2e"). Plain words; never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The path sentence of a node the read starts at.</summary>
    internal const string ContextMapPathAnchor = "The read starts here.";

    /// <summary>The path sentence of a node no starting point reaches.</summary>
    internal const string ContextMapPathNone = "Nothing on the map links this to a starting point.";

    /// <summary>How a node is reached from the nearest starting point.</summary>
    /// <param name="start">The starting point's label.</param>
    /// <param name="hops">One phrase per step, in order.</param>
    /// <returns>"Starts at Tab manager, then TabManager.ts (implemented in)."</returns>
    internal static string ContextMapPathSentence(string start, IEnumerable<string> hops) =>
        $"Starts at {start}, then {string.Join(", then ", hops)}.";

    /// <summary>One step of the path sentence.</summary>
    /// <param name="label">The node reached.</param>
    /// <param name="word">The edge's words.</param>
    /// <returns>"TabManager.ts (implemented in)".</returns>
    internal static string ContextMapPathHop(string label, string word) => $"{label} ({word})";

    /// <summary>The inspector section: what the node is.</summary>
    internal const string ContextInspWhat = "What it is";

    /// <summary>The inspector section: why the node is in the map.</summary>
    internal const string ContextInspWhy = "Why it is in this map";

    /// <summary>The inspector section: a conflict.</summary>
    internal const string ContextInspConflict = "A conflict";

    /// <summary>The inspector section: where the node comes from.</summary>
    internal const string ContextInspSource = "Where it comes from";

    /// <summary>The inspector section: what the node is linked to.</summary>
    internal const string ContextInspConnected = "Connected to";

    /// <summary>The sources table's columns.</summary>
    internal const string ContextInspSrcSource = "Source";

    /// <summary>The sources table's when column.</summary>
    internal const string ContextInspSrcWhen = "When";

    /// <summary>The sources table's how sure column.</summary>
    internal const string ContextInspSrcSure = "How sure";

    /// <summary>Nothing else is linked to the node.</summary>
    internal const string ContextInspConnectedNone = "Nothing else is linked to this.";

    /// <summary>How sure the tool is about a node or a link, in words (a glyph goes with them in the view).</summary>
    /// <param name="confirmed">Whether a person confirmed it.</param>
    /// <param name="provenance">Who or what found it.</param>
    /// <param name="confidence">How sure the tool is, 0 to 1.</param>
    /// <returns>"confirmed by a person", "found by the tool" or "suggested by AI, 74% sure, not confirmed".</returns>
    internal static string ContextInspSure(bool confirmed, string provenance, double confidence) => (confirmed, provenance) switch
    {
        (true, _) or (_, "human") => "confirmed by a person",
        (_, "model") => string.Create(CultureInfo.InvariantCulture, $"suggested by AI, {(int)Math.Round(confidence * 100, MidpointRounding.AwayFromZero)}% sure, not confirmed"),
        _ => "found by the tool",
    };

    /// <summary>The conflict a decision is part of.</summary>
    /// <param name="other">The decision it disagrees with.</param>
    /// <param name="node">What both apply to.</param>
    /// <returns>The sentence.</returns>
    internal static string ContextInspConflictBody(string other, string node) =>
        $"This decision disagrees with {other}. Both apply to {node}. Settle which one wins before approving.";

    /// <summary>The conflict on a node two decisions disagree about.</summary>
    /// <param name="first">One decision.</param>
    /// <param name="second">The other decision.</param>
    /// <returns>The sentence.</returns>
    internal static string ContextInspConflictNode(string first, string second) =>
        $"Two decisions disagree about this: {first} and {second}. Settle which one wins before approving.";

    /// <summary>The note on a link nobody confirmed.</summary>
    internal const string ContextInspNotConfirmed = "not confirmed";

    /// <summary>The accept button.</summary>
    internal const string ContextInspAccept = "Accept";

    /// <summary>The reject button.</summary>
    internal const string ContextInspReject = "Reject";

    /// <summary>The button that copies a file's path.</summary>
    internal const string ContextInspCopyPath = "Copy path";

    /// <summary>What Accept or Reject saved.</summary>
    internal const string ContextInspSaved = "Saved to the draft map.";

    /// <summary>The source was never read.</summary>
    internal const string ContextInspNotSynced = "not synced yet";

    /// <summary>When a source was last read.</summary>
    /// <param name="age">How long ago.</param>
    /// <returns>"synced 2 h ago".</returns>
    internal static string ContextInspSynced(TimeSpan age) => age.TotalMinutes switch
    {
        < 1 => "synced just now",
        < 60 => string.Create(CultureInfo.InvariantCulture, $"synced {(int)age.TotalMinutes} min ago"),
        < 1440 => string.Create(CultureInfo.InvariantCulture, $"synced {(int)age.TotalHours} h ago"),
        _ => string.Create(CultureInfo.InvariantCulture, $"synced {(int)age.TotalDays} d ago"),
    };

    /// <summary>The name of a source row's origin when the node comes from no synced source.</summary>
    /// <param name="provenance">Who or what found it.</param>
    /// <returns>The words.</returns>
    internal static string ContextInspOrigin(string provenance) => provenance switch
    {
        "human" => "A person",
        "model" => "AI",
        "deterministic" => "The tool's scan",
        _ => provenance,
    };
}
