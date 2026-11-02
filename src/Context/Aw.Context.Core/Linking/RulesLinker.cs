using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linking;

/// <summary>
/// The rules-first linker (spec §13): issue keys and URLs, repo paths, identifier tokens and quoted titles in a record's scrubbed text, matched to the map's nodes and to other records.
/// Pure and idempotent: the same text and graph give the same links (an issue key whose record isn't known links by the key, so fetch order never changes the result). Plain English words never link.
/// </summary>
public static partial class RulesLinker
{
    private const double Exact = 1.0;
    private const double ByPath = 0.8;
    private const double ByName = 0.7;
    private const double ByPhrase = 0.5;

    // Words that name nothing on their own: a label made only of these ('General settings') is not a phrase worth finding in prose.
    private static readonly HashSet<string> _commonWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "and", "or", "of", "to", "in", "on", "for", "with", "new", "old", "main", "other", "general", "common", "default", "basic", "all", "any", "more", "less",
        "settings", "setting", "options", "option", "items", "item", "list", "page", "home", "view", "data", "info", "misc", "support", "help", "tools", "tool", "things", "stuff",
    };

    /// <summary>Links one record's text.</summary>
    /// <param name="record">The record the text belongs to (it never links to itself).</param>
    /// <param name="text">The sanitized, scrubbed text.</param>
    /// <param name="graph">The map to link into.</param>
    /// <param name="tracker">Every intent record the sync knows, for the issue-key and URL rules, and the profile's tracker teams: only their keys link.</param>
    /// <param name="pins">The approved pins' ids and aliases, for the alias rule (see <see cref="Aliases"/>); none runs no alias rule.</param>
    /// <returns>The links, ordered by target then rule.</returns>
    public static IReadOnlyList<IntentLink> Link(RecordKey record, string text, GraphSnapshot graph, (IReadOnlyList<IntentRecord> Known, IReadOnlySet<string> Teams) tracker, IReadOnlyList<PinAliases> pins)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(tracker.Known);
        ArgumentNullException.ThrowIfNull(tracker.Teams);
        ArgumentNullException.ThrowIfNull(pins);
        var run = new Run(record, Bounded(text), graph, tracker.Known, tracker.Teams);
        run.Urls();
        run.Paths();
        run.QuotedTitles();
        run.IssueKeys();
        run.IdentifierTokens();
        run.Names();
        run.Aliases(pins);
        return run.Result();
    }

    private sealed partial class Run(RecordKey record, string text, GraphSnapshot graph, IReadOnlyList<IntentRecord> known, IReadOnlySet<string> teams)
    {
        private readonly Dictionary<(string Target, LinkRule Rule), IntentLink> _found = [];
        private readonly StringBuilder _rest = new(text);

        public IReadOnlyList<IntentLink> Result() =>
            [.. _found.Values.OrderBy(l => l.Target.Value, StringComparer.Ordinal).ThenBy(l => l.Rule)];

        public void Urls()
        {
            foreach (var match in LinearUrl.Matches(_rest.ToString()).Where(m => IsTeamKey(m.Groups["key"].Value, teams)).ToList())
            {
                LinkRecord(match, key: match.Groups["key"].Value, LinkRule.Url);
                Mask(match);
            }
        }

        public void Paths()
        {
            foreach (var match in RepoPath.Matches(_rest.ToString()).ToList())
            {
                if (graph.Nodes.FirstOrDefault(n => n.Key.Value == "code:" + match.Value) is { } code)
                {
                    Add(code.Key.Value, LinkRule.Path, ByPath, match.Value);
                    LinkImplemented(code, match.Value);
                    Mask(match);
                }
            }
        }

        public void QuotedTitles()
        {
            var byLabel = graph.Nodes.Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision)).ToLookup(n => n.Label.Trim(), StringComparer.OrdinalIgnoreCase);
            foreach (var match in Quoted.Matches(_rest.ToString()).ToList())
            {
                var nodes = byLabel[match.Groups["title"].Value.Trim()].ToList();
                if (nodes.Count == 1)
                {
                    Add(nodes[0].Key.Value, LinkRule.QuotedTitle, ByName, match.Groups["title"].Value);
                    Mask(match);
                }
            }
        }

        public void IssueKeys()
        {
            foreach (var match in IssueKey.Matches(_rest.ToString()).ToList())
            {
                LinkRecord(match, match.Value, LinkRule.IssueKey);
            }
        }

        public void IdentifierTokens()
        {
            var identifiers = Identifiers(graph);
            foreach (var match in Token.Matches(_rest.ToString()).ToList())
            {
                if (identifiers.TryGetValue(match.Value, out var nodes) && nodes.Count == 1)
                {
                    Add(nodes.First(), LinkRule.Identifier, ByName, match.Value);
                }
            }
        }

        // A suggestion, not a fact: a feature's name (label, or an alias of it) in the text as whole words, when it names exactly one feature. The longest phrase wins where phrases overlap.
        // Glossary aliases (feature:glossary/<slug>) are offered through graph.Aliases like any other alias once they are in the map.
        public void Names()
        {
            var phrases = Phrases(graph);
            var words = Words(_rest.ToString());
            foreach (var phrase in phrases.Keys.OrderByDescending(p => p.Length).ThenBy(p => p, StringComparer.Ordinal))
            {
                if (!words.Contains(phrase, StringComparison.Ordinal))
                {
                    continue;
                }

                words = words.Replace(phrase, " # ", StringComparison.Ordinal);
                if (phrases[phrase].Count == 1)
                {
                    Add(phrases[phrase].First(), LinkRule.Name, ByPhrase, phrase.Trim());
                }
            }
        }

        private void LinkRecord(Match m, string key, LinkRule rule)
        {
            if (!IsTeamKey(key, teams))
            {
                return;
            }

            // A record not fetched yet is linked by its issue key, which the overlay resolves through the record's aliases whenever it arrives: the links never depend on fetch order.
            if (RecordFor(key, known, teams) is { } target)
            {
                if (target.Key.Value != record.Value)
                {
                    Add("record:" + target.Key.Value, rule, Exact, m.Value);
                }
            }
            else
            {
                Add("record:" + key, rule, Exact, m.Value);
            }
        }

        private void LinkImplemented(GraphNode code, string span)
        {
            foreach (var edge in graph.Edges.Where(e => e.Type == EdgeType.Implements && e.From.Value == code.Key.Value && e.Provenance != Provenance.Model))
            {
                Add(edge.To.Value, LinkRule.Path, ByPath, span);
            }
        }

        private void Add(string target, LinkRule rule, double confidence, string span)
        {
            if (NodeKey.TryFrom(target, out var key) && !_found.ContainsKey((target, rule)))
            {
                _found[(target, rule)] = new IntentLink(record, key, rule, confidence, Hash(span));
            }
        }

        private void Mask(Match match) => _ = _rest.Remove(match.Index, match.Length).Insert(match.Index, new string(' ', match.Length));
    }

    private static bool IsTeamKey(string issueKey, IReadOnlySet<string> teams) => teams.Contains(issueKey[..issueKey.IndexOf('-', StringComparison.Ordinal)]);

    private static IntentRecord? RecordFor(string issueKey, IReadOnlyList<IntentRecord> known, IReadOnlySet<string> teams) =>
        IsTeamKey(issueKey, teams) ? known.FirstOrDefault(r => r.Aliases.Contains(issueKey, StringComparer.Ordinal)) : null;

    private static Dictionary<string, SortedSet<string>> Identifiers(GraphSnapshot graph)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        void Offer(string candidate, string target)
        {
            if (IsIdentifier(candidate))
            {
                if (!map.TryGetValue(candidate, out var set))
                {
                    map[candidate] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                _ = set.Add(target);
            }
        }

        foreach (var node in graph.Nodes.Where(n => n.Key.Kind is not (NodeKind.Record or NodeKind.Decision)))
        {
            Offer(node.Key.Name, node.Key.Value);
            Offer(Tail(node.Key.Name), node.Key.Value);
            Offer(node.Label, node.Key.Value);
        }

        foreach (var alias in graph.Aliases.Where(a => a.Alias.Kind is not (NodeKind.Record or NodeKind.Decision)))
        {
            Offer(alias.Alias.Name, alias.Target.Value);
            Offer(Tail(alias.Alias.Name), alias.Target.Value);
        }

        return map;
    }

    private static string Words(string text) => " " + string.Join(' ', Word.Matches(text).Select(m => m.Value.ToLowerInvariant())) + " ";

    private static Dictionary<string, SortedSet<string>> Phrases(GraphSnapshot graph)
    {
        var map = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        void Offer(string candidate, string target)
        {
            var words = Word.Matches(candidate).Select(m => m.Value.ToLowerInvariant()).ToList();
            if (words.Count < 2 || string.Concat(words).Length < 4 || words.TrueForAll(_commonWords.Contains))
            {
                return;
            }

            var phrase = " " + string.Join(' ', words) + " ";
            if (!map.TryGetValue(phrase, out var set))
            {
                map[phrase] = set = new SortedSet<string>(StringComparer.Ordinal);
            }

            _ = set.Add(target);
        }

        foreach (var node in graph.Nodes.Where(n => n.Key.Kind == NodeKind.Feature))
        {
            Offer(node.Label, node.Key.Value);
        }

        foreach (var alias in graph.Aliases.Where(a => a.Target.Kind == NodeKind.Feature && a.Alias.Kind is not (NodeKind.Record or NodeKind.Decision)))
        {
            Offer(alias.Alias.Name, alias.Target.Value);
        }

        return map;
    }

    private static string Tail(string name) => name[(name.LastIndexOf('/') + 1)..];

    /// <summary>Whether a candidate looks like a code identifier (a separator or a camelCase hump) and so can't be a plain English word.</summary>
    private static bool IsIdentifier(string candidate)
    {
        if (candidate.Length < 4 || !Token.IsMatch(candidate) || Token.Match(candidate).Value.Length != candidate.Length)
        {
            return false;
        }

        return candidate.AsSpan().IndexOfAny("-_.") is > 0
            || candidate.Zip(candidate.Skip(1)).Any(p => char.IsLower(p.First) && char.IsUpper(p.Second));
    }

    private static string Hash(string span) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(span)));

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Word { get; }

    [GeneratedRegex(@"https://linear\.app/[^/\s]+/issue/(?<key>" + LinearRecordKeys.TeamKeyPattern + @"-[0-9]+)(?:/[^\s]*)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LinearUrl { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9-])" + LinearRecordKeys.TeamKeyPattern + @"-[0-9]+(?![A-Za-z0-9-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IssueKey { get; }

    [GeneratedRegex(@"(?<![\w/.\-])(?:[\w.\-]+/)+[\w\-]+(?:\.[A-Za-z0-9]+)+(?![\w/\-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepoPath { get; }

    [GeneratedRegex("(?<![\\w])[\"'`“‘](?<title>[^\"'`“”‘’\\n]{2,120})[\"'`”’](?![\\w])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Quoted { get; }

    [GeneratedRegex(@"(?<![A-Za-z0-9_\-.])[A-Za-z_][A-Za-z0-9_\-.]*[A-Za-z0-9_](?![A-Za-z0-9_\-]|\.[A-Za-z0-9_])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Token { get; }
}
