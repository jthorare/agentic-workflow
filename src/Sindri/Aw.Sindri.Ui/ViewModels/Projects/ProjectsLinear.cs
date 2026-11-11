using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// What the Projects page says about Linear's state, in plain words. Two served facts can both be true at once: the viewer's name (an identity Sindri stored when Linear last answered)
/// and "Linear needs a token" (the profile lists teams but names no token). Without a key the name is shown as last seen, and the sentence says what is wrong and when Linear was last read.
/// </summary>
internal static class ProjectsLinear
{
    /// <summary>True when the served code says the profile has no Linear token.</summary>
    /// <param name="list">The served list.</param>
    /// <returns>True for the missing-key code.</returns>
    internal static bool IsKeyMissing(ProjectList list) => string.Equals(list.EmptyCode, "linear-key-missing", StringComparison.Ordinal);

    /// <summary>True when the page offers Connect Linear: no key, or Linear not connected at all.</summary>
    /// <param name="list">The served list.</param>
    /// <returns>True when Connect Linear helps.</returns>
    internal static bool OffersConnect(ProjectList list) => IsKeyMissing(list) || string.Equals(list.EmptyCode, "linear-missing", StringComparison.Ordinal);

    /// <summary>The sentence the page shows: the served one, except the missing-key one, which is said in plain words with the last read (no terminal command).</summary>
    /// <param name="list">The served list.</param>
    /// <param name="now">The instant "ago" counts from.</param>
    /// <returns>The sentence, or null.</returns>
    internal static string? Sentence(ProjectList list, DateTimeOffset now)
    {
        if (!IsKeyMissing(list))
        {
            return list.Empty;
        }

        // Only Linear's own rows: a newer GitHub sync says nothing about when Linear was last read.
        var last = list.Freshness.Where(static f => f.Source.StartsWith("linear", StringComparison.Ordinal)).Select(static f => f.At ?? default).DefaultIfEmpty().Max();
        return Labels.ProjectsNoKey(last == default ? string.Empty : Labels.AgoWords(now - last));
    }

    /// <summary>The freshness line: each source, then who Linear says the owner is ("last seen as" without a key).</summary>
    /// <param name="list">The served list.</param>
    /// <returns>The line.</returns>
    internal static string Freshness(ProjectList list)
    {
        var parts = list.Freshness.Select(f => Labels.ProjectFreshnessPart(f.Source, f.Text)).ToList();
        if (list.Viewer is { } viewer)
        {
            parts.Add(IsKeyMissing(list) ? Labels.ProjectViewerLastSeen(viewer) : Labels.ProjectViewer(viewer));
        }

        return string.Join(" · ", parts);
    }
}
