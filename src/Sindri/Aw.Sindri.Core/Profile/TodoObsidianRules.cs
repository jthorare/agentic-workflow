using System.Text.Json;
using Aw.Kernel;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The checks of 'todo.obsidian' (todo spec §4.5, §6): the vault is one the profile's library names, and the note is a vault-relative .md path that stays inside it, outside every dot folder and every deny glob of that vault.
/// A bad value is SND-TODO-002 with the key path. The note's own file (a symlink, more than one hard link, not writable) is looked at where the file can be read, not here.
/// </summary>
public static class TodoObsidianRules
{
    private const int MaxNote = 400;

    /// <summary>Checks the profile's 'todo.obsidian'.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The problem, or null when the key is absent or good.</returns>
    internal static Error? Check(SindriProfile profile) =>
        profile.Todo?.Obsidian is { } key ? Problem(key.Vault, key.Note, profile.Context) : null;

    /// <summary>Checks a vault name and a note path against the vaults of a profile's 'context' section.</summary>
    /// <param name="vault">The vault name.</param>
    /// <param name="note">The note path inside it.</param>
    /// <param name="context">The profile's 'context' section, or null.</param>
    /// <returns>SND-TODO-002 naming the key path and what is wrong, or null.</returns>
    public static Error? Problem(string? vault, string? note, JsonElement? context)
    {
        var vaults = Vaults(context);
        if (string.IsNullOrEmpty(vault))
        {
            return Invalid("todo.obsidian.vault", "is empty (fix: name one of your library vaults)");
        }

        if (!vaults.TryGetValue(vault, out var deny))
        {
            return Invalid("todo.obsidian.vault", vaults.Count == 0
                ? $"'{Shown(vault)}' is not a vault: the profile has no library vaults (fix: add one in Settings → Library first)"
                : $"'{Shown(vault)}' is not one of your library vaults: {string.Join(", ", vaults.Keys.Order(StringComparer.Ordinal))}");
        }

        return NotePath(note) is { } bad ? Invalid("todo.obsidian.note", bad) : Denied(note ?? string.Empty, deny);
    }

    /// <summary>What is wrong with a note path by its shape alone, or null.</summary>
    /// <param name="note">The path.</param>
    /// <returns>The reason, as the end of "todo.obsidian.note …", or null.</returns>
    public static string? NotePath(string? note) => NotePath(note, OperatingSystem.IsWindows());

    /// <summary>What is wrong with a note path by its shape alone, with the platform given.</summary>
    /// <param name="note">The path.</param>
    /// <param name="windows">True on Windows, where a ':' in the second place is a drive letter; elsewhere only 'X:/' is an absolute path and 'a:b.md' is a plain file name.</param>
    /// <returns>The reason, as the end of "todo.obsidian.note …", or null.</returns>
    internal static string? NotePath(string? note, bool windows)
    {
        if (string.IsNullOrEmpty(note))
        {
            return "is empty (fix: a path inside the vault, such as Todo.md)";
        }

        var segments = note.Split('/');
        return true switch
        {
            _ when note.Length > MaxNote => "is too long",
            _ when note.Any(static c => char.IsControl(c)) => "has a control character",
            _ when note.Contains('\\', StringComparison.Ordinal) => "must use '/' separators",
            _ when note.StartsWith('/') || (note.Length >= 2 && note[1] == ':' && (windows || (note.Length >= 3 && note[2] == '/'))) => "must be relative to the vault, not an absolute path",
            _ when segments.Any(static s => s.Length == 0) => "has an empty folder name",
            _ when segments.Contains("..", StringComparer.Ordinal) || segments.Contains(".", StringComparer.Ordinal) => "must stay inside the vault: no '.' or '..' in the path",
            _ when segments[..^1].Any(static s => s.StartsWith('.')) => "must not be inside a dot folder (such as .obsidian)",
            _ when !note.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || segments[^1].Length <= ".md".Length => "must be a .md file",
            _ => null,
        };
    }

    private static Error? Denied(string note, IReadOnlyList<string> deny)
    {
        var hit = deny.FirstOrDefault(glob => PathGlob.Matches(glob, note));
        return hit is null ? null : Invalid("todo.obsidian.note", $"is matched by the vault's deny glob '{Shown(hit)}', which keeps it out of the library (fix: pick another note or change the glob)");
    }

    // the vault names of 'context.sources.library.vaults' with their deny globs
    private static Dictionary<string, IReadOnlyList<string>> Vaults(JsonElement? context)
    {
        var found = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var vaults = Descend(context, "sources", "library", "vaults");
        foreach (var vault in vaults is { ValueKind: JsonValueKind.Array } list ? list.EnumerateArray().ToList() : [])
        {
            if (Descend(vault, "name") is { ValueKind: JsonValueKind.String } name && name.GetString() is { Length: > 0 } text)
            {
                found[text] = Descend(vault, "deny") is { ValueKind: JsonValueKind.Array } deny
                    ? [.. deny.EnumerateArray().Where(static d => d.ValueKind == JsonValueKind.String).Select(static d => d.GetString() ?? string.Empty)]
                    : [];
            }
        }

        return found;
    }

    private static JsonElement? Descend(JsonElement? from, params string[] path)
    {
        var at = from;
        foreach (var key in path)
        {
            if (at is not { ValueKind: JsonValueKind.Object } here || !here.TryGetProperty(key, out var next))
            {
                return null;
            }

            at = next;
        }

        return at;
    }

    private static string Shown(string text) => Text.SafeText.Line(Text.SafeText.Escape(text), 60);

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.TodoNoteKey.With($"{keyPath}: {problem}");
}
