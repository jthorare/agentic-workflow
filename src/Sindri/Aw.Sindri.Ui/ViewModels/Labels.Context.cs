using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page (screens spec §4.4). Words shared by the page's stages live here; each stage adds its own words to its own Labels.Context*.cs partial. Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page head before any map has been read.</summary>
    internal const string ContextNoMap = "No context map yet";

    /// <summary>The anchor box's name.</summary>
    internal const string ContextAnchor = "Ask or look up";

    /// <summary>The anchor box's hint.</summary>
    internal const string ContextAnchorHint = "Ask a question, or type a topic such as feature:name";

    /// <summary>The head of a read: how many topics it was asked about. The map's version is in the answer's closed disclosure, not up front.</summary>
    /// <param name="anchors">How many anchors were found.</param>
    /// <returns>The head line.</returns>
    internal static string ContextHead(int anchors) =>
        string.Create(CultureInfo.InvariantCulture, $"{anchors} {(anchors == 1 ? "topic" : "topics")}");

    /// <summary>The head of a read that came from a question: how many topics the question found.</summary>
    /// <param name="topics">How many anchors the question resolved to.</param>
    /// <returns>The head line.</returns>
    internal static string ContextFromQuestion(int topics) =>
        string.Create(CultureInfo.InvariantCulture, $"From your question: {topics} {(topics == 1 ? "topic" : "topics")}");

    /// <summary>The word for an anchor the map does not hold.</summary>
    internal const string ContextNotFound = "not found";

    /// <summary>The suggested anchors' name.</summary>
    internal const string ContextSuggestions = "Did you mean";

    /// <summary>The word for an untrusted label.</summary>
    internal const string ContextUntrusted = "untrusted";

    /// <summary>The word for an anchor node in a band.</summary>
    internal const string ContextAnchorTag = "topic";

    /// <summary>The note while an item's context is read.</summary>
    /// <param name="item">The item's id.</param>
    /// <returns>The note.</returns>
    internal static string ContextItemLoading(string item) => $"Reading the context of {item}…";

    /// <summary>The note when the item has no frozen scope: the map shown is its repo's.</summary>
    /// <param name="item">The item's id.</param>
    /// <param name="repo">The item's repo, or null when it is not known.</param>
    /// <returns>The note.</returns>
    internal static string ContextItemUnscoped(string item, string? repo) =>
        $"No scope yet for {item}; showing " + (repo is null ? "the whole map" : $"{repo}'s map");

    /// <summary>The note when the item has a frozen scope: its starting points are in the box.</summary>
    /// <param name="item">The item's id.</param>
    /// <returns>The note.</returns>
    internal static string ContextItemScoped(string item) => $"Context of {item}, from its frozen scope";

    /// <summary>The note when the item's context could not be read.</summary>
    /// <param name="item">The item's id.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The note.</returns>
    internal static string ContextItemFailed(string item, string reason) => $"Could not read the context of {item}: {reason}";

    /// <summary>The reason when there is no connection to the control plane.</summary>
    internal const string ContextItemUnreachable = "the control plane is not reachable";

    /// <summary>The line when a question or a name is not on the map.</summary>
    /// <param name="asked">What was typed.</param>
    /// <returns>"Nothing on the map matches 'Tab groups'. Try a topic such as feature:name."</returns>
    internal static string ContextNothingMatches(string asked) =>
        $"Nothing on the map matches '{asked}'. Try a topic such as feature:name.";

    /// <summary>The line when a name is the name of several items.</summary>
    /// <param name="asked">What was typed.</param>
    /// <returns>"'Pane' names several items. Pick one."</returns>
    internal static string ContextSeveralMatch(string asked) => $"'{asked}' names several items. Pick one.";

    /// <summary>A refusal of a search in the app's own words: the miss and the several-match codes are worded here, any other keeps the server's sentence without its command-line hint.</summary>
    /// <param name="code">The refusal's code.</param>
    /// <param name="message">The server's sentence.</param>
    /// <param name="asked">What was typed.</param>
    /// <returns>The words for the banner.</returns>
    internal static string ContextRefusal(string code, string message, string asked)
    {
        if (string.Equals(code, "SND-CONTEXT-003", StringComparison.Ordinal))
        {
            return ContextNothingMatches(asked);
        }

        if (string.Equals(code, "SND-CONTEXT-037", StringComparison.Ordinal))
        {
            return ContextSeveralMatch(asked);
        }

        var hint = message.IndexOf(" (fix:", StringComparison.Ordinal);
        return hint < 0 ? message : message[..hint];
    }
}
