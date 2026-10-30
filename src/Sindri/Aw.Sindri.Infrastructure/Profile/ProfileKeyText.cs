using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// The text edit behind 'profile set': one scalar key of a profile's YAML, every other byte as it was. An existing scalar line is replaced where it stands (a trailing comment stays), a missing key is appended at the end of its parent's block and missing parents are
/// created in block style with two-space indents. A flow-style parent, a value that isn't a plain or quoted scalar and a key that holds a map or list are layouts Sindri won't edit (the same rule as <see cref="YamlSourceTokenProfileWriter"/>).
/// </summary>
internal static class ProfileKeyText
{
    private static readonly HashSet<string> _booleanWords = new(StringComparer.Ordinal) { "true", "false", "yes", "no", "y", "n" };

    /// <summary>Sets <paramref name="keyPath"/> to <paramref name="value"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The profile file's text.</param>
    /// <param name="keyPath">The dotted key.</param>
    /// <param name="value">The value as typed.</param>
    /// <returns>The edited text (equal to <paramref name="text"/> when the key already has the value); SND-CLI-003 for a bad key or a value over several lines; SND-PROFILE-002 for a layout it won't edit.</returns>
    internal static Result<string> Edit(string text, string keyPath, string value)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(keyPath);
        ArgumentNullException.ThrowIfNull(value);
        var parts = keyPath.Split('.');
        if (parts.Any(static p => !IsSegment(p)))
        {
            return SindriErrors.InvalidInput.With($"'{keyPath}' is not a dotted key (letters, digits, '-' and '_' between the dots, such as context.models.labeling.pick)");
        }

        if (value.Contains('\n', StringComparison.Ordinal) || value.Contains('\r', StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With("the value must be one line");
        }

        var lines = new List<string>(text.Split('\n'));
        var cr = text.Contains("\r\n", StringComparison.Ordinal) ? "\r" : string.Empty;
        var scalar = Scalar(value);
        var parent = -1;   // the line of the key whose block is searched; -1 is the document itself
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var path = string.Join('.', parts[..(i + 1)]);
            var found = Find(lines, parent, parts[i]);
            if (found < 0)
            {
                return Create(lines, parent, (parts[i..], string.Join('.', parts[..i])), scalar, cr);
            }

            if (Split(lines[found]).Value.Length > 0)
            {
                return Refuse(path, "is written in flow style or holds a single value, not a block map");
            }

            parent = found;
        }

        var at = Find(lines, parent, parts[^1]);
        return at < 0 ? Create(lines, parent, (parts[^1..], string.Join('.', parts[..^1])), scalar, cr) : Replace(lines, at, keyPath, scalar);
    }

    /// <summary>The YAML scalar for <paramref name="value"/>: booleans and numbers plain, a plain-safe word plain, anything else quoted.</summary>
    /// <param name="value">The value as typed.</param>
    /// <returns>The scalar text.</returns>
    internal static string Scalar(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return IsBoolean(value) || IsNumber(value) || IsPlainWord(value) ? value : YamlProfileLines.Quote(value);
    }

    private static Result<string> Replace(List<string> lines, int at, string keyPath, string scalar)
    {
        var (head, existing, suffix) = Split(lines[at]);
        if (existing.Length == 0 && Block(lines, at).Indent >= 0)
        {
            return Refuse(keyPath, "holds a map or list, not a single value");
        }

        if (existing.Length > 0 && existing[0] is '{' or '[' or '|' or '>' or '&' or '*' or '!')
        {
            return Refuse(keyPath, "holds a map, a list or a value Sindri won't edit, not a plain or quoted value");
        }

        if (Same(existing, scalar))
        {
            return string.Join('\n', lines);
        }

        lines[at] = head + " " + scalar + suffix;
        return string.Join('\n', lines);
    }

    // The line's `key:` head, the value after it without its comment, and what follows the value (spaces, comment, carriage return) so a replacement keeps it.
    private static (string Head, string Value, string Suffix) Split(string line)
    {
        var colon = ColonIndex(line);
        var rest = line[(colon + 1)..];
        var end = rest.Length;
        char? quote = null;
        for (var i = 0; i < rest.Length; i++)
        {
            var c = rest[i];
            if (quote is { } open)
            {
                quote = c == open ? null : quote;
            }
            else if (c is '"' or '\'' && rest[..i].Trim().Length == 0)
            {
                quote = c;
            }
            else if (c == '#' && (i == 0 || rest[i - 1] is ' ' or '\t'))
            {
                end = i;
                break;
            }
        }

        var value = rest[..end].Trim();
        var cut = rest[..end].TrimEnd().Length;
        return (line[..(colon + 1)], value, rest[cut..]);
    }

    private static Result<string> Create(List<string> lines, int parent, (string[] Parts, string ParentPath) missing, string scalar, string cr)
    {
        var block = Block(lines, parent);
        if (block.List)
        {
            return Refuse(missing.ParentPath, "is a list, not a map");
        }

        var after = parent + 1;
        for (var i = block.End - 1; i > parent; i--)
        {
            if (!IsBlankOrComment(lines[i]))
            {
                after = i + 1;
                break;
            }
        }

        var indent = block.Indent >= 0 ? block.Indent : parent < 0 ? 0 : YamlProfileLines.Indent(lines[parent]) + 2;
        var added = missing.Parts.Select((part, k) => new string(' ', indent + (2 * k)) + part + ":" + ((k == missing.Parts.Length - 1) ? " " + scalar : string.Empty) + cr);
        lines.InsertRange(after, added);
        return string.Join('\n', lines);
    }

    // The block under `parent` (-1: the whole document): the indent of its first content line (-1 when empty), where it ends and whether it is a list.
    private static (int Indent, int End, bool List) Block(List<string> lines, int parent)
    {
        var parentIndent = parent < 0 ? -1 : YamlProfileLines.Indent(lines[parent]);
        var indent = -1;
        var list = false;
        for (var i = parent + 1; i < lines.Count; i++)
        {
            if (IsBlankOrComment(lines[i]))
            {
                continue;
            }

            var isItem = IsListItem(lines[i]);
            if (YamlProfileLines.Indent(lines[i]) <= parentIndent)
            {
                return (indent, i, indent < 0 && isItem && YamlProfileLines.Indent(lines[i]) == parentIndent);   // `key:` followed by `- item` at the key's own indent
            }

            if (indent < 0)
            {
                indent = YamlProfileLines.Indent(lines[i]);
                list = isItem;
            }
        }

        return (indent, lines.Count, list);
    }

    private static int Find(List<string> lines, int parent, string key)
    {
        var block = Block(lines, parent);
        for (var i = parent + 1; i < block.End && block.Indent >= 0 && !block.List; i++)
        {
            if (!IsBlankOrComment(lines[i]) && YamlProfileLines.Indent(lines[i]) == block.Indent && KeyOf(lines[i]) == key)
            {
                return i;
            }
        }

        return -1;
    }

    private static string? KeyOf(string line)
    {
        var colon = ColonIndex(line);
        return colon < 0 ? null : line[..colon].Trim().Trim('"', '\'');
    }

    // The first colon that ends a key: followed by a space, a carriage return or the end of the line.
    private static int ColonIndex(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == ':' && (i + 1 == line.Length || line[i + 1] is ' ' or '\r'))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool Same(string existing, string scalar)
    {
        var plain = Unquote(scalar);
        return string.Equals(existing, scalar, StringComparison.Ordinal)
            || (string.Equals(Unquote(existing), plain, StringComparison.Ordinal) && IsPlainWord(plain) && !IsBoolean(plain));
    }

    private static string Unquote(string token) =>
        token.Length >= 2 && token[0] is '"' or '\'' && token[^1] == token[0] ? token[1..^1] : token;

    private static Result<string> Refuse(string keyPath, string why) =>
        SindriErrors.ProfileInvalid.With($"{keyPath} {why}, a layout Sindri won't edit (fix: change it in the profile file by hand)");

    private static bool IsBlankOrComment(string line) => line.Trim().Length == 0 || line.TrimStart().StartsWith('#');

    private static bool IsListItem(string line) => line.Trim() == "-" || line.TrimStart().StartsWith("- ", StringComparison.Ordinal);

    private static bool IsSegment(string part) => part.Length > 0 && part.All(static c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    // -?digits[.digits], no leading zero (007 would read as 7).
    private static bool IsNumber(string value)
    {
        var digits = value.StartsWith('-') ? value[1..] : value;
        var dot = digits.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? digits : digits[..dot];
        return whole.Length > 0 && whole.All(char.IsAsciiDigit) && (whole.Length == 1 || whole[0] != '0')
            && (dot < 0 || (digits.Length > dot + 1 && digits[(dot + 1)..].All(char.IsAsciiDigit)));
    }

    // A word YAML reads back as the same string: starts with a letter, then letters, digits and . _ - /, and isn't one of the words that mean null or a boolean.
    private static bool IsPlainWord(string value) =>
        value.Length > 0 && char.IsAsciiLetter(value[0]) && value.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or '/')
        && !value.ToLowerInvariant().Equals("null", StringComparison.Ordinal) && !IsBooleanWord(value);

    private static bool IsBoolean(string value) => value.Equals("true", StringComparison.Ordinal) || value.Equals("false", StringComparison.Ordinal);

    // The words YAML 1.1 reads as a boolean in any case.
    private static bool IsBooleanWord(string value) => _booleanWords.Contains(value.ToLowerInvariant());
}
