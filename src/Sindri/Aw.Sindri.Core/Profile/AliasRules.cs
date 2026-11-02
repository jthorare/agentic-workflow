using System.Globalization;
using System.Text;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>The rules for a pin's 'aliases' (concept pins spec §3.4): shape, count, and the clash check in the matcher's normal form.</summary>
internal static class AliasRules
{
    private const int MaxAlias = 60;
    private const int MaxAliases = 20;

    // Keep in step with AnchorMatch._small.
    private static readonly HashSet<string> _smallWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "done", "for", "from", "how", "i", "if", "in", "is", "it", "its", "of", "on",
        "or", "s", "the", "this", "that", "to", "was", "what", "when", "where", "which", "who", "why", "with",
    };

    internal static Error? Aliases(string path, IReadOnlyList<string>? aliases)
    {
        if (aliases is { Count: > MaxAliases })
        {
            return IndexRules.Invalid(path, string.Create(CultureInfo.InvariantCulture, $"must hold at most {MaxAliases} aliases"));
        }

        for (var i = 0; i < (aliases?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when aliases is null
            var alias = aliases![i];
            var at = string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]");
            if (alias.Length is 0 or > MaxAlias || !alias.Any(char.IsLetterOrDigit) || alias.Any(IndexRules.Invisible))
            {
                return IndexRules.Invalid(at, string.Create(CultureInfo.InvariantCulture, $"must be 1 to {MaxAlias} characters with a letter or digit and no control, format or line-separator characters"));
            }

            if (Normalized(alias).Length == 0)
            {
                return IndexRules.Invalid(at, "is made only of words the matcher skips ('the', 'of', ...), so it could never match");
            }
        }

        return null;
    }

    /// <summary>No alias may equal, ignoring case and a plural 's', another pin's id, name or alias (the matcher's own normal form).</summary>
    internal static Error? Clashes(string path, IReadOnlyList<ConceptSettings>? concepts, IReadOnlyList<ConceptSettings> inherited)
    {
        var all = new List<(string Path, ConceptSettings Pin, bool Inherited)>();
        all.AddRange(inherited.Select((pin, i) => (string.Create(CultureInfo.InvariantCulture, $"index.concepts[{i}]"), pin, true)));
        all.AddRange((concepts ?? []).Select((pin, i) => (string.Create(CultureInfo.InvariantCulture, $"{path}[{i}]"), pin, false)));
        foreach (var (at, pin, isInherited) in all)
        {
            for (var a = 0; a < (pin.Aliases?.Count ?? 0); a++)
            {
                // why: the loop bound is zero when aliases is null
                var form = Normalized(pin.Aliases![a]);
                var other = all.Where(o => !ReferenceEquals(o.Pin, pin) && !(isInherited && o.Inherited)).Select(o => Taken(o.Path, o.Pin, form)).FirstOrDefault(static t => t is not null);
                if (other is not null)
                {
                    return IndexRules.Invalid(string.Create(CultureInfo.InvariantCulture, $"{at}.aliases[{a}]"), $"'{pin.Aliases[a]}' clashes with {other} (aliases match without case or plural)");
                }
            }
        }

        return null;
    }

    private static string? Taken(string at, ConceptSettings pin, string form)
    {
        if (pin.Id is { } id && Normalized(id.Value) == form)
        {
            return $"{at}.id '{id.Value}'";
        }

        if (pin.Name is { } name && Normalized(name) == form)
        {
            return $"{at}.name '{name}'";
        }

        for (var i = 0; i < (pin.Aliases?.Count ?? 0); i++)
        {
            // why: the loop bound is zero when aliases is null
            if (Normalized(pin.Aliases![i]) == form)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{at}.aliases[{i}] '{pin.Aliases[i]}'");
            }
        }

        return null;
    }

    // The matcher's form (AnchorMatch.Words in Aw.Context.Core, which this project can't reference): lower-case letter and digit
    // runs, small words left out, a plural 's' dropped, then sorted so word order doesn't matter.
    private static string Normalized(string text)
    {
        var words = new SortedSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();
        foreach (var c in text + " ")
        {
            if (char.IsLetterOrDigit(c))
            {
                _ = word.Append(char.ToLowerInvariant(c));
                continue;
            }

            if (word.Length > 0)
            {
                var w = word.ToString();
                _ = word.Clear();
                if (!_smallWords.Contains(w) && !_smallWords.Contains(w = Singular(w)))
                {
                    _ = words.Add(w);
                }
            }
        }

        return string.Join(' ', words);
    }

    private static string Singular(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;
}
