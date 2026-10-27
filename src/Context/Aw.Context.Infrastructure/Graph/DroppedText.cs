using System.Globalization;
using System.Text;
using Aw.Context.Core.Roles;

namespace Aw.Context.Infrastructure.Graph;

/// <summary>How map_version.dropped is stored (issue 1161): one line per ignored role, "repo", "role" and the count joined by U+001F; null for a version that recorded none, an empty string for one that recorded an empty list.</summary>
internal static class DroppedText
{
    private const char Unit = '\u001f';

    public static string? Write(IReadOnlyList<RoleDropped>? dropped)
    {
        if (dropped is null)
        {
            return null;
        }

        var text = new StringBuilder();
        foreach (var d in dropped)
        {
            _ = text.Append(d.Repo).Append(Unit).Append(d.Role).Append(Unit).Append(d.Files.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return text.ToString();
    }

    // A line that doesn't read (a hand edit) is left out rather than failing the version list.
    public static IReadOnlyList<RoleDropped>? Read(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var read = new List<RoleDropped>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(Unit);
            if (parts.Length == 3 && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var files))
            {
                read.Add(new RoleDropped(parts[0], parts[1], files));
            }
        }

        return read;
    }
}
