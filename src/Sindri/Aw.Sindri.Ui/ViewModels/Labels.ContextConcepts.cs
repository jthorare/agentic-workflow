using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Context page's concept band, the first layer of the map.</summary>
internal static partial class Labels
{
    /// <summary>The head over the concept layer.</summary>
    internal const string ContextConceptsHead = "Context · concepts";

    /// <summary>The first step of the breadcrumb, and the layer's accessible name.</summary>
    internal const string ContextConceptsCrumb = "Concepts";

    /// <summary>The list of concepts' accessible name.</summary>
    internal const string ContextConceptsListName = "The product's concepts";

    /// <summary>The button that goes up one layer.</summary>
    internal const string ContextConceptsBack = "Back";

    /// <summary>The mark on a concept a person set.</summary>
    internal const string ContextConceptSetByPerson = "Set by a person";

    /// <summary>The line when the map has no concepts yet; the map itself opens as before.</summary>
    internal const string ContextConceptsEmpty = "The map has no concepts yet, so the whole map is shown.";

    /// <summary>The line over the cards when no concept has terms yet.</summary>
    internal const string ContextConceptsNoTerms = "No terms yet.";

    /// <summary>The line over the cards when the glossary is refused.</summary>
    /// <param name="code">The error code.</param>
    /// <param name="message">The refusal's message.</param>
    /// <returns>"Terms are not available (SND-CONTEXT-041): ...".</returns>
    internal static string ContextConceptsTermsUnavailable(string code, string message) => $"Terms are not available ({code}): {message}";

    /// <summary>The line while the concepts are still being built; the map itself opens as before.</summary>
    internal const string ContextConceptsBuilding = "The concepts are still being built, so the whole map is shown.";

    /// <summary>The line when the concepts cannot be read; the map itself opens as before.</summary>
    /// <param name="code">The error code.</param>
    /// <returns>"Concepts are not available (SND-CONTEXT-041), so the whole map is shown."</returns>
    internal static string ContextConceptsUnavailable(string code) => $"Concepts are not available ({code}), so the whole map is shown.";

    /// <summary>What a concept holds, in people's words.</summary>
    /// <param name="features">Features.</param>
    /// <param name="records">Records.</param>
    /// <param name="decisions">Decisions.</param>
    /// <param name="code">Code files.</param>
    /// <returns>"4 features · 3 records · 2 decisions · 40 code files"; a kind with none is left out, and "nothing yet" when all are none.</returns>
    internal static string ContextConceptSize(int features, int records, int decisions, int code)
    {
        var parts = new (string Kind, int Count)[] { ("feature", features), ("record", records), ("decision", decisions), ("code", code) }
            .Where(p => p.Count > 0).Select(p => ContextWholeKindCount(p.Kind, p.Count)).ToList();
        return parts.Count == 0 ? "nothing yet" : string.Join(" · ", parts);
    }

    /// <summary>A concept card's top terms.</summary>
    /// <param name="shown">The terms to name, best first.</param>
    /// <param name="more">How many more there are.</param>
    /// <returns>"Terms: shift · swap · roster +2".</returns>
    internal static string ContextConceptTerms(IReadOnlyList<string> shown, int more)
    {
        ArgumentNullException.ThrowIfNull(shown);
        return "Terms: " + string.Join(" · ", shown) + (more > 0 ? " +" + more.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    /// <summary>A concept's accessible name.</summary>
    /// <param name="name">The concept's name.</param>
    /// <param name="size">The size line.</param>
    /// <returns>"Self scheduling, 4 features. Press Enter to open."</returns>
    internal static string ContextConceptTileName(string name, string size) => $"{name}, {size}. Press Enter to open.";
}
