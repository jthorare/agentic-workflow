using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Graph.Ops;

/// <summary>Follows a node rename through the '{{old.field}}' references in a config.</summary>
internal static partial class ReferenceRename
{
    [GeneratedRegex("(?<open>\\{\\{\\s*)(?<id>[a-z][a-z0-9_]*)(?=[.\\s}])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Reference { get; }

    /// <summary>Follows a rename through a condition's 'when' text: 'old.field' outside quotes becomes 'next.field' (the '{{old.field}}' form is covered by <see cref="Apply"/>).</summary>
    public static void ApplyWhen(JsonObject? config, string old, string next)
    {
        if (config?["when"] is JsonValue value && value.TryGetValue<string>(out var text))
        {
            config["when"] = JsonValue.Create(RewriteWhen(text, old, next));
        }
    }

    // why: a quoted string in an expression is a literal, never a reference; a reference is 'node.field', so the id must be followed by a dot and not follow one
    private static string RewriteWhen(string text, string old, string next)
    {
        var result = new System.Text.StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c is '"' or '\'')
            {
                i = CopyLiteral(text, i, result);
            }
            else if (StartsWord(text, i))
            {
                i = RewriteWord(text, i, old, next, result);
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString();
    }

    // why: a word starts at a lower-case letter that neither continues another word nor follows a dot (a field name)
    private static bool StartsWord(string text, int i) =>
        IsIdentStart(text[i]) && (i == 0 || (!IsIdentPart(text[i - 1]) && text[i - 1] != '.'));

    private static int CopyLiteral(string text, int i, System.Text.StringBuilder result)
    {
        var end = text.IndexOf(text[i], i + 1);
        var stop = end < 0 ? text.Length : end + 1;
        result.Append(text, i, stop - i);
        return stop;
    }

    private static int RewriteWord(string text, int i, string old, string next, System.Text.StringBuilder result)
    {
        var j = i;
        while (j < text.Length && IsIdentPart(text[j]))
        {
            j++;
        }

        var word = text.AsSpan(i, j - i);
        var isReference = j < text.Length && text[j] == '.' && word.SequenceEqual(old);
        result.Append(isReference ? next : word.ToString());
        return j;
    }

    private static bool IsIdentStart(char c) => c is >= 'a' and <= 'z';

    private static bool IsIdentPart(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';

    // why: the '{{old.field}}' form follows in every config string; the bare form in a condition's 'when' follows through ApplyWhen
    public static void Apply(JsonNode? node, string old, string next)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(static p => p.Key).ToList())
                {
                    Visit(obj[key], text => obj[key] = JsonValue.Create(text), old, next);
                }

                break;
            case JsonArray list:
                for (var i = 0; i < list.Count; i++)
                {
                    var at = i;
                    Visit(list[at], text => list[at] = JsonValue.Create(text), old, next);
                }

                break;
            default:
                break;
        }
    }

    private static void Visit(JsonNode? item, Action<string> replace, string old, string next)
    {
        if (item is JsonValue value && value.TryGetValue<string>(out var text))
        {
            replace(Reference.Replace(text, m => string.Equals(m.Groups["id"].Value, old, StringComparison.Ordinal) ? m.Groups["open"].Value + next : m.Value));
            return;
        }

        Apply(item, old, next);
    }
}
