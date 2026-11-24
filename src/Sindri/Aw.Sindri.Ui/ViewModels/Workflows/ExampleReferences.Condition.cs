namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// Reads the references in a block's example text (<see cref="ExampleReferences"/>, continued): a condition, the way the condition tokenizer reads one
/// (Tokenizer in Aw.Sindri.Core/Graph/Expressions). A bare word such as agent.drifted, or a braced {{agent.drifted}}, is a reference; a "string", a number,
/// and, or, not, true, false, null and exists( are not.
/// </summary>
internal static partial class ExampleReferences
{
    // why: the words Tokenizer.Word in Aw.Sindri.Core reads as keywords, not references; ExampleReferencesCopyTests in Aw.Sindri.Tests fails when the two differ
    private static readonly HashSet<string> _keywords = new(["and", "or", "not", "true", "false", "null"], StringComparer.Ordinal);

    // The references of a condition, in order.
    private static List<string> ConditionReferences(string text)
    {
        var found = new List<string>();
        var at = 0;
        while (at < text.Length)
        {
            at = Token(text, at, found);
        }

        return found;
    }

    // Reads the token that starts at the index, keeps it when it is a reference, and returns where the next token starts.
    private static int Token(string text, int at, List<string> found) => text[at] switch
    {
        '"' => AfterString(text, at + 1),
        '{' when at + 1 < text.Length && text[at + 1] == '{' => Braced(text, at + 2, found),
        _ when StartsNumber(text, at) => AfterNumber(text, at + 1),
        var c when char.IsAsciiLetter(c) || c == '_' => Word(text, at, found),
        _ => at + 1,
    };

    private static bool StartsNumber(string text, int at) =>
        char.IsAsciiDigit(text[at]) || (text[at] == '-' && at + 1 < text.Length && char.IsAsciiDigit(text[at + 1]));

    // A "string" with backslash escapes: nothing in it is a reference.
    private static int AfterString(string text, int at)
    {
        while (at < text.Length && text[at] != '"')
        {
            at += text[at] == '\\' ? 2 : 1;
        }

        return Math.Min(at + 1, text.Length);
    }

    private static int AfterNumber(string text, int at)
    {
        while (at < text.Length && (char.IsAsciiDigit(text[at]) || text[at] == '.'))
        {
            at++;
        }

        return at;
    }

    private static int Braced(string text, int at, List<string> found)
    {
        var close = text.IndexOf("}}", at, StringComparison.Ordinal);
        if (close < 0)
        {
            return text.Length;
        }

        var name = text[at..close].Trim();
        if (name.Length > 0)
        {
            found.Add(name);
        }

        return close + 2;
    }

    private static int Word(string text, int at, List<string> found)
    {
        var start = at;
        while (at < text.Length && (char.IsAsciiLetterOrDigit(text[at]) || text[at] is '_' or '.' or '-'))
        {
            at++;
        }

        var word = text[start..at];
        var rest = text.AsSpan(at).TrimStart();
        var exists = string.Equals(word, "exists", StringComparison.Ordinal) && !rest.IsEmpty && rest[0] == '(';
        if (!exists && !_keywords.Contains(word))
        {
            found.Add(word);
        }

        return at;
    }
}
