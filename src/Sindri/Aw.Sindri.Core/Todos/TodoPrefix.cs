using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Todos;

/// <summary>Resolves the id a CLI user typed (todo spec §4.4 "Ids"): a full id, a unique prefix among the todos the verb acts on, or the note's '^td-…' form.</summary>
public static class TodoPrefix
{
    private const string NotePrefix = "^td-";

    /// <summary>Resolves <paramref name="typed"/> among <paramref name="rows"/>.</summary>
    /// <param name="typed">What was typed: 'td_01jc5a', 'td_01jc5a8xq0n4w7ms2t9r3vbk6e' or '^td-01jc5a'.</param>
    /// <param name="verb">The verb, which decides which rows count.</param>
    /// <param name="rows">Every todo.</param>
    /// <returns>The id, SND-TODO-404 when none matches, or SND-TODO-010 naming the matches when several do.</returns>
    public static Result<TodoId> Resolve(string typed, TodoCliVerb verb, IReadOnlyList<TodoRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return Resolve(typed, verb, [.. rows.Select(static r => new TodoCandidate(r.Id, r.DoneAt is not null, r.Removed))]);
    }

    /// <summary>Resolves <paramref name="typed"/> among <paramref name="rows"/>, given only what the choice needs.</summary>
    /// <param name="typed">What was typed.</param>
    /// <param name="verb">The verb, which decides which rows count.</param>
    /// <param name="rows">Every todo.</param>
    /// <returns>The id, SND-TODO-404 when none matches, or SND-TODO-010 naming the matches when several do.</returns>
    public static Result<TodoId> Resolve(string typed, TodoCliVerb verb, IReadOnlyList<TodoCandidate> rows)
    {
        ArgumentNullException.ThrowIfNull(typed);
        ArgumentNullException.ThrowIfNull(rows);
        var wanted = typed.Trim().ToLowerInvariant();
        if (wanted.StartsWith(NotePrefix, StringComparison.Ordinal))
        {
            wanted = "td_" + wanted[NotePrefix.Length..];
        }

        var seen = rows.Where(row => Acts(verb, row)).Select(row => row.Id).ToList();
        var exact = seen.Where(id => string.Equals(id.Value, wanted, StringComparison.Ordinal)).ToList();
        var matches = exact.Count == 1 ? exact : [.. seen.Where(id => wanted.Length > 0 && id.Value.StartsWith(wanted, StringComparison.Ordinal))];
        return matches.Count switch
        {
            0 => SindriErrors.TodoNotFound.With($"No todo {typed}."),
            1 => matches[0],
            _ => SindriErrors.TodoAmbiguous.With(string.Create(CultureInfo.InvariantCulture, $"{typed} matches {matches.Count} todos: {string.Join(", ", matches.Select(id => id.Value))}")),
        };
    }

    private static bool Acts(TodoCliVerb verb, TodoCandidate row) => verb switch
    {
        TodoCliVerb.Show => true,
        TodoCliVerb.Reopen => row.Done && !row.Removed,
        TodoCliVerb.Restore => row.Removed,
        TodoCliVerb.Edit or TodoCliVerb.Done or TodoCliVerb.Remove or TodoCliVerb.Keep => !row.Done && !row.Removed,
        _ => false,
    };
}
