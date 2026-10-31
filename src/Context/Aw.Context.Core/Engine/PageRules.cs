using System.Globalization;
using Aw.Kernel;

namespace Aw.Context.Core.Engine;

/// <summary>The rules for named pages at write (pages spec §3.1): pure, over the pages and the current map's facts. A read of an approved profile never runs them: the overlay falls back instead.</summary>
public static class PageRules
{
    /// <summary>The most pages a profile holds.</summary>
    public const int MaxPages = 200;

    /// <summary>The most actions a page holds.</summary>
    public const int MaxActions = 40;

    /// <summary>The most concepts an action relates to.</summary>
    public const int MaxConcepts = 20;

    /// <summary>The longest name, after control and format characters are stripped.</summary>
    public const int MaxNameLength = 80;

    private const string ConceptPrefix = "concept:";
    private const int FixDistance = 3;

    /// <summary>Checks pages against the map.</summary>
    /// <param name="pages">The pages as the owner wrote them.</param>
    /// <param name="facts">The current map's concept ids, surface and action keys and their repos.</param>
    /// <returns>Every problem found, in page order; empty when the pages hold.</returns>
    public static IReadOnlyList<Error> Check(IReadOnlyList<PagePin> pages, PageFacts facts)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(facts);
        var errors = new List<Error>();
        if (pages.Count > MaxPages)
        {
            errors.Add(ContextErrors.PageOverCap.With($"index.pages: {Count(pages.Count)} pages; at most {Count(MaxPages)}"));
        }

        var seen = new HashSet<PageId>();
        for (var p = 0; p < pages.Count; p++)
        {
            var at = string.Create(CultureInfo.InvariantCulture, $"index.pages[{p}]");
            CheckPage(pages[p], at, facts, errors);
            if (!seen.Add(pages[p].Id))
            {
                var first = IndexOf(pages, pages[p].Id);
                errors.Add(ContextErrors.PageIdDuplicate.With($"{at}.id: '{pages[p].Id.Value}' is already index.pages[{Count(first)}]'s"));
            }
        }

        return errors;
    }

    private static void CheckPage(PagePin page, string at, PageFacts facts, List<Error> errors)
    {
        CheckName(page.Name, $"{at}.name", errors);
        if (page.SurfaceKey is { } surface)
        {
            if (!facts.SurfaceRepos.TryGetValue(surface, out var repos))
            {
                errors.Add(ContextErrors.PageKeyUnknown.With($"{at}.surface: '{surface}' is not in the current map (fix: sindri context page list --suggested, or pick it in the app)"));
            }
            else if (page.Repo is null && repos.Count > 1)
            {
                errors.Add(ContextErrors.PageRepoAmbiguous.With($"{at}: this page is in {string.Join(" and ", repos)}: say which (fix: --repo)"));
            }
        }

        if (page.Actions.Count > MaxActions)
        {
            errors.Add(ContextErrors.PageOverCap.With($"{at}: {Count(page.Actions.Count)} actions; a page holds at most {Count(MaxActions)}"));
        }

        var seen = new HashSet<PageActionId>();
        for (var a = 0; a < page.Actions.Count; a++)
        {
            var action = page.Actions[a];
            var actionAt = $"{at}.actions[{Count(a)}]";
            CheckName(action.Name, $"{actionAt}.name", errors);
            if (!seen.Add(action.Id))
            {
                var first = page.Actions.Select(x => x.Id).ToList().IndexOf(action.Id);
                errors.Add(ContextErrors.PageIdDuplicate.With($"{actionAt}.id: '{action.Id.Value}' is already actions[{Count(first)}]'s"));
            }

            if (action.ActionKey is { } key && !facts.ActionRepos.ContainsKey(key))
            {
                errors.Add(ContextErrors.PageKeyUnknown.With($"{actionAt}.action: '{key}' is not in the current map (fix: sindri context page list --suggested, or pick it in the app)"));
            }

            CheckConcepts(action, actionAt, facts, errors);
        }
    }

    private static void CheckConcepts(PageActionPin action, string actionAt, PageFacts facts, List<Error> errors)
    {
        if (action.Concepts.Count > MaxConcepts)
        {
            errors.Add(ContextErrors.PageOverCap.With($"{actionAt}: {Count(action.Concepts.Count)} concepts; at most {Count(MaxConcepts)}"));
        }

        for (var c = 0; c < action.Concepts.Count; c++)
        {
            var written = action.Concepts[c];
            var bare = written.StartsWith(ConceptPrefix, StringComparison.Ordinal) ? written[ConceptPrefix.Length..] : written;
            if (ConceptId.TryFrom(bare, out var id) && facts.ConceptIds.Contains(id))
            {
                continue;
            }

            var near = Nearest(bare, facts.ConceptIds);
            var fix = near is null ? string.Empty : $" (fix: try {near})";
            errors.Add(ContextErrors.PageConceptUnknown.With($"{actionAt}.concepts[{Count(c)}]: '{written}' is no concept in the current map{fix}"));
        }
    }

    private static void CheckName(string name, string at, List<Error> errors)
    {
        var clean = string.Concat(name.Where(static ch => !char.IsControl(ch) && char.GetUnicodeCategory(ch) != UnicodeCategory.Format));
        if (clean.Length == 0)
        {
            errors.Add(ContextErrors.PageNameInvalid.With($"{at}: is empty"));
        }
        else if (clean.Length > MaxNameLength)
        {
            errors.Add(ContextErrors.PageNameInvalid.With($"{at}: {Count(clean.Length)} characters; at most {Count(MaxNameLength)}"));
        }
        else if (char.IsWhiteSpace(clean[0]) || char.IsWhiteSpace(clean[^1]))
        {
            errors.Add(ContextErrors.PageNameInvalid.With($"{at}: starts or ends with a space"));
        }
    }

    private static string? Nearest(string written, IReadOnlySet<ConceptId> ids) => NearestId(written, ids.Select(static i => i.Value));

    /// <summary>The candidate closest to a written id, for a "fix: try" hint.</summary>
    /// <param name="written">What the caller wrote.</param>
    /// <param name="candidates">The ids that exist.</param>
    /// <returns>The nearest id within three edits (ties go to the first in ordinal order), or null.</returns>
    internal static string? NearestId(string written, IEnumerable<string> candidates)
    {
        string? best = null;
        var bestDistance = FixDistance + 1;
        foreach (var id in candidates.Order(StringComparer.Ordinal))
        {
            var distance = Distance(written, id);
            if (distance < bestDistance)
            {
                best = id;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var diagonal = row[0];
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }

        return row[b.Length];
    }

    private static int IndexOf(IReadOnlyList<PagePin> pages, PageId id)
    {
        for (var i = 0; i < pages.Count; i++)
        {
            if (pages[i].Id.Equals(id))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);
}
