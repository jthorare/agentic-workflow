using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The Reviews header's library line (PR review page spec §6.1, §6.3).</summary>
/// <param name="State">"ok", "reading", "empty" or "error".</param>
/// <param name="Docs">The design docs stored.</param>
/// <param name="WithErrors">The docs stored with an error.</param>
/// <param name="UnreadableRoots">The folders that couldn't be read.</param>
/// <param name="Text">Sindri's words for it.</param>
/// <param name="Unreadable">Null when every folder reads; else each folder that couldn't be read, with why ("&lt;root&gt; couldn't be read: &lt;reason&gt;", §6.3).</param>
public sealed record ReviewLibrary(string State, int Docs, int WithErrors, int UnreadableRoots, string Text, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReviewUnreadableRoot>? Unreadable = null)
{
    /// <summary>Decides the state and its words.</summary>
    /// <param name="facts">What the library holds.</param>
    /// <returns>The header line.</returns>
    public static ReviewLibrary From(ReviewLibraryFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var (state, text) = true switch
        {
            _ when facts.Reading => ("reading", "Library: reading…"),
            _ when facts.UnreadableRoots.Count > 0 => ("error", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Library: {facts.Docs} docs, {facts.UnreadableRoots.Count} {(facts.UnreadableRoots.Count == 1 ? "folder can't" : "folders can't")} be read")),
            _ when facts.Docs == 0 => ("empty", "Library: no design docs yet"),
            _ => ("ok", string.Create(System.Globalization.CultureInfo.InvariantCulture, $"Library: {facts.Docs} docs, {facts.WithErrors} with errors")),
        };
        return new ReviewLibrary(state, facts.Docs, facts.WithErrors, facts.UnreadableRoots.Count, text, facts.UnreadableRoots.Count == 0 ? null : facts.UnreadableRoots);
    }
}
