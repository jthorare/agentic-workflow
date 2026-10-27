using System.Text;

namespace Aw.Context.Core.Glossary;

/// <summary>Splits identifiers and labels into lower-cased words: camelCase, PascalCase, snake_case, kebab-case and digits all separate.</summary>
internal static class GlossaryWords
{
    internal const int MinLetters = 3;

    internal static List<string> Split(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsLetter(text[i]))
            {
                Flush(word, words);
                continue;
            }

            if (word.Length > 0 && StartsWord(text, i))
            {
                Flush(word, words);
            }

            word.Append(char.ToLowerInvariant(text[i]));
        }

        Flush(word, words);
        return words;
    }

    // A capital after a lower-case letter starts a word ('fooBar'), and so does the last capital of an acronym ('HTTPServer').
    private static bool StartsWord(string text, int i)
    {
        if (!char.IsUpper(text[i]))
        {
            return false;
        }

        return char.IsLower(text[i - 1]) || (i + 1 < text.Length && char.IsUpper(text[i - 1]) && char.IsLower(text[i + 1]));
    }

    private static void Flush(StringBuilder word, List<string> words)
    {
        if (word.Length >= MinLetters)
        {
            words.Add(word.ToString());
        }

        word.Clear();
    }
}
