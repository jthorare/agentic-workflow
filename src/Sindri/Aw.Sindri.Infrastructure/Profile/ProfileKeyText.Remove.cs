using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>The removal half of <see cref="ProfileKeyText"/> ('todo note stop'): the key's lines go, every other line stays.</summary>
internal static partial class ProfileKeyText
{
    /// <summary>Removes <paramref name="keyPath"/> from <paramref name="text"/>: the key's line and the block under it go, comments and every other line stay, and a parent map left with nothing under it goes too.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="keyPath">The dotted key.</param>
    /// <returns>The edited text (equal to <paramref name="text"/> when the key or a parent of it is absent); SND-CLI-003 for a bad key; SND-PROFILE-002 for a layout it won't edit.</returns>
    internal static Result<string> Remove(string text, string keyPath)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(keyPath);
        var parts = keyPath.Split('.');
        if (parts.Any(static p => !IsSegment(p)))
        {
            return SindriErrors.InvalidInput.With($"'{keyPath}' is not a dotted key (letters, digits, '-' and '_' between the dots, such as todo.obsidian)");
        }

        var lines = new List<string>(text.Split('\n'));
        var walked = Walk(lines, parts);
        if (walked.IsError)
        {
            return walked.Error;
        }

        var (parents, at) = walked.Value;
        if (at < 0)
        {
            return text;
        }

        var refused = Unremovable(lines, at, keyPath);
        if (refused is not null)
        {
            return refused;
        }

        lines.RemoveRange(at, LastLine(lines, at) + 1 - at);
        for (var i = parents.Count - 1; i >= 0 && Block(lines, parents[i]).Indent < 0 && Split(lines[parents[i]]).Value.Length == 0; i--)
        {
            lines.RemoveAt(parents[i]);
        }

        return string.Join('\n', lines);
    }

    // the line of each parent on the way to the key, and the key's own line (-1 when the key or a parent is absent)
    private static Result<(List<int> Parents, int At)> Walk(List<string> lines, string[] parts)
    {
        var parents = new List<int>();
        var parent = -1;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var found = Find(lines, parent, parts[i]);
            if (found < 0)
            {
                return (parents, -1);
            }

            if (Split(lines[found]).Value.Length > 0)
            {
                return Refuse(string.Join('.', parts[..(i + 1)]), "is written in flow style or holds a single value, not a block map").Error;
            }

            parents.Add(found);
            parent = found;
        }

        return (parents, Find(lines, parent, parts[^1]));
    }

    // a key written in flow style, or a list at its own indent, is a layout Sindri won't remove
    private static Error? Unremovable(List<string> lines, int at, string keyPath)
    {
        var existing = Split(lines[at]).Value;
        var block = Block(lines, at);
        var flow = existing.Length > 0 && existing[0] is '{' or '[' or '|' or '>' or '&' or '*' or '!';
        return flow || (existing.Length == 0 && block.Indent < 0 && block.List)
            ? Refuse(keyPath, "is written in flow style or as a list at its own indent, a layout Sindri won't remove").Error
            : null;
    }

    // the last content line of the key's block (the key's own line when it has none); trailing comments and blank lines belong to what follows
    private static int LastLine(List<string> lines, int at)
    {
        var block = Block(lines, at);
        var last = at;
        for (var i = at + 1; block.Indent >= 0 && i < block.End; i++)
        {
            last = IsBlankOrComment(lines[i]) ? last : i;
        }

        return last;
    }
}
