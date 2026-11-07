using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>Skill ids as the owner reads them.</summary>
internal static class SkillWords
{
    /// <summary>An id in words: 'needsWork' is "needs work", 'office-hours' is "office hours", 'pass' is "pass".</summary>
    /// <param name="outcome">The id as served (camelCase).</param>
    /// <returns>Lower-case words, split at each capital.</returns>
    internal static string Outcome(string outcome)
    {
        var words = new StringBuilder(outcome.Length + 4);
        foreach (var c in outcome)
        {
            _ = char.IsUpper(c) ? words.Append(' ').Append(char.ToLowerInvariant(c)) : words.Append(c is '-' or '_' ? ' ' : c);
        }

        return words.ToString().Trim();
    }
}
