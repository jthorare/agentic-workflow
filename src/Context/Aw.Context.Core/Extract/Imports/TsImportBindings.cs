using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract.Imports;

/// <summary>The names the import statements of a TS or JS file bind (comments are ignored; type-only imports bind no value and are left out).</summary>
internal static partial class TsImportBindings
{
    internal static IReadOnlyList<ImportBinding> Read(string text)
    {
        try
        {
            return [.. Statement.Matches(Comments.Strip(text)).SelectMany(m => Bindings(m.Groups["c"].Value.Trim(), m.Groups["s"].Value))];
        }
        catch (RegexMatchTimeoutException)
        {
            return [];
        }
    }

    // import D, { a as b, type T } from 'x'   import * as ns from 'x'   import D from 'x'
    private static IEnumerable<ImportBinding> Bindings(string clause, string specifier)
    {
        if (clause.StartsWith("type ", StringComparison.Ordinal))
        {
            return [];   // import type: nothing at run time
        }

        var open = clause.IndexOf('{', StringComparison.Ordinal);
        var close = clause.IndexOf('}', StringComparison.Ordinal);
        var head = (open < 0 ? clause : clause[..open]).Trim().TrimEnd(',').Trim();
        var named = open < 0 || close < open ? [] : clause[(open + 1)..close].Split(',').SelectMany(item => Named(item, specifier));
        return Head(head, specifier).Concat(named);
    }

    private static IEnumerable<ImportBinding> Head(string head, string specifier)
    {
        if (head.StartsWith("* as ", StringComparison.Ordinal))
        {
            return [new ImportBinding(head[5..].Trim(), string.Empty, specifier, ImportKind.Namespace)];
        }

        return head.Length > 0 && Identifier.IsMatch(head) ? [new ImportBinding(head, "default", specifier, ImportKind.Default)] : [];
    }

    private static IEnumerable<ImportBinding> Named(string item, string specifier)
    {
        var words = item.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || (string.Equals(words[0], "type", StringComparison.Ordinal) && words.Length > 1 && !string.Equals(words[1], "as", StringComparison.Ordinal)))
        {
            return [];
        }

        return words.Length switch
        {
            3 when string.Equals(words[1], "as", StringComparison.Ordinal) => [new ImportBinding(words[2], words[0], specifier, ImportKind.Named)],
            1 => [new ImportBinding(words[0], words[0], specifier, ImportKind.Named)],
            _ => [],
        };
    }

    [GeneratedRegex("\\bimport\\s+(?<c>[^'\";]+?)\\s+from\\s*(?<q>[\"'])(?<s>[^\"'\\r\\n]+)\\k<q>", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Statement { get; }

    [GeneratedRegex("^[A-Za-z_$][\\w$]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Identifier { get; }
}
