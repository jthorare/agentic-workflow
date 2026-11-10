using System.Text;

namespace Aw.Sindri.Core.Graph.Docs;

/// <summary>Keyword search over the workflow doc pages: ranks by the question's words in titles, headings and text. No embeddings.</summary>
public static class WorkflowDocSearch
{
    private const double TitleWeight = 6;
    private const double HeadingWeight = 3;
    private const int BodyCap = 3;

    private static readonly HashSet<string> _stopWords = new(
        ["a", "an", "the", "and", "or", "of", "to", "in", "on", "at", "by", "for", "with", "is", "it", "are", "be", "do", "does", "i", "my", "me", "you", "we", "can", "what", "when", "while", "how", "this", "that", "then", "if", "as"],
        StringComparer.Ordinal);

    private static readonly string[] _suffixes = ["ing", "es", "ed", "s"];

    /// <summary>Finds the pages that best answer <paramref name="question"/>.</summary>
    /// <param name="pages">The pages to search.</param>
    /// <param name="question">The question, in plain words.</param>
    /// <param name="max">The most hits to return.</param>
    /// <returns>The pages with a match, best first; ties go by page slug.</returns>
    public static IReadOnlyList<WorkflowDocHit> Search(IReadOnlyList<WorkflowDocPage> pages, string question, int max = 5)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(question);
        var terms = Terms(question).Distinct(StringComparer.Ordinal).ToList();
        if (terms.Count == 0 || max <= 0 || pages.Count == 0)
        {
            return [];
        }

        var indexed = pages.Select(Index).ToList();
        var rarities = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var term in terms)
        {
            var seen = indexed.Count(p => p.Seen(term));
            if (seen > 0)   // why: a term no page has would divide by zero (Infinity * weight 0 = NaN) and empty every score
            {
                rarities[term] = Math.Log(1 + ((double)indexed.Count / seen));
            }
        }

        var hits = new List<WorkflowDocHit>();
        foreach (var page in indexed)
        {
            var score = 0.0;
            foreach (var (term, rarity) in rarities)
            {
                score += rarity * page.Weight(term);
            }

            if (score > 0)
            {
                hits.Add(new WorkflowDocHit(page.Page, score));
            }
        }

        return [.. hits.OrderByDescending(static h => h.Score).ThenBy(static h => h.Page.Slug, StringComparer.Ordinal).Take(max)];
    }

    private static IndexedPage Index(WorkflowDocPage page)
    {
        var headings = new List<string>();
        var body = new List<string>();
        foreach (var line in page.Text.Split('\n'))
        {
            (line.TrimStart().StartsWith('#') ? headings : body).AddRange(Terms(line));
        }

        return new IndexedPage(page, [.. Terms(page.Slug).Concat(Terms(page.Title))], headings, body);
    }

    private static IEnumerable<string> Terms(string text)
    {
        var word = new StringBuilder();
        foreach (var c in text.Append(' '))
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(char.ToLowerInvariant(c));
                continue;
            }

            if (word.Length > 0)
            {
                var token = word.ToString();
                word.Clear();
                if (token.Length > 1 && !_stopWords.Contains(token))
                {
                    yield return Stem(token);
                }
            }
        }
    }

    // why: "schedule", "scheduled", "schedules" and "scheduling" must meet; a light suffix strip is enough for these docs
    private static string Stem(string word)
    {
        foreach (var suffix in _suffixes)
        {
            if (word.Length >= suffix.Length + 3 && word.EndsWith(suffix, StringComparison.Ordinal) && !(suffix == "s" && word.EndsWith("ss", StringComparison.Ordinal)))
            {
                word = word[..^suffix.Length];
                break;
            }
        }

        return word.Length > 4 && word.EndsWith('e') ? word[..^1] : word;
    }

    private sealed class IndexedPage(WorkflowDocPage page, IReadOnlyList<string> title, IReadOnlyList<string> headings, IReadOnlyList<string> body)
    {
        public WorkflowDocPage Page { get; } = page;

        public bool Seen(string term) => title.Contains(term, StringComparer.Ordinal) || headings.Contains(term, StringComparer.Ordinal) || body.Contains(term, StringComparer.Ordinal);

        public double Weight(string term) =>
            (title.Contains(term, StringComparer.Ordinal) ? TitleWeight : 0)
            + (headings.Contains(term, StringComparer.Ordinal) ? HeadingWeight : 0)
            + Math.Min(body.Count(t => string.Equals(t, term, StringComparison.Ordinal)), BodyCap);
    }
}
