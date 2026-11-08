using Aw.Kernel;

namespace Aw.Sindri.Core.Todos;

/// <summary>Resolves the id a CLI user typed (todo spec §4.4 "Ids"): a full id, a unique prefix among the todos the verb acts on, or the note's '^td-…' form.</summary>
public static class TodoPrefix
{
    /// <summary>Resolves <paramref name="typed"/> among <paramref name="rows"/>.</summary>
    /// <param name="typed">What was typed: 'td_01jc5a', 'td_01jc5a8xq0n4w7ms2t9r3vbk6e' or '^td-01jc5a'.</param>
    /// <param name="verb">The verb, which decides which rows count.</param>
    /// <param name="rows">Every todo.</param>
    /// <returns>The id, SND-TODO-404 when none matches, or SND-TODO-010 naming the matches when several do.</returns>
    public static Result<TodoId> Resolve(string typed, TodoCliVerb verb, IReadOnlyList<TodoRow> rows)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(rows);
        throw new NotSupportedException($"TodoPrefix.Resolve({verb}): make TodoPrefixTests pass");
    }
}
