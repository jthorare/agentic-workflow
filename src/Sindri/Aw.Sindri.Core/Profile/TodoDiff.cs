namespace Aw.Sindri.Core.Profile;

/// <summary>The 'todo:' key paths a profile sets, for 'profile approve' to compare. An absent key has no path, so adding one shows as a change.</summary>
internal static class TodoDiff
{
    internal static IEnumerable<(string Key, string Value)> Keys(TodoSettings? todo)
    {
        if (todo?.Obsidian is not { } obsidian)
        {
            yield break;
        }

        if (obsidian.Vault is { } vault)
        {
            yield return ("todo.obsidian.vault", vault);
        }

        if (obsidian.Note is { } note)
        {
            yield return ("todo.obsidian.note", note);
        }
    }
}
