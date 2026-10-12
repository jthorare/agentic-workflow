namespace Aw.Sindri.Core.Workflow.Validation;

/// <summary>
/// Where each node of a recipe file starts, by node path, as the reader recorded it from the file as written (a
/// short form's long-form children point at the short form's node). A path joins map keys with '.' and list
/// indices with '[i]': 'states.orphan', 'transitions[2].gates[0].use', 'states.produce.timeout.after'. The root is ''.
/// </summary>
public sealed class SourcePositions
{
    private readonly IReadOnlyDictionary<string, SourcePosition> _byPath;

    /// <summary>Over the positions the reader recorded.</summary>
    /// <param name="byPath">Each node's position by path.</param>
    public SourcePositions(IReadOnlyDictionary<string, SourcePosition> byPath)
    {
        ArgumentNullException.ThrowIfNull(byPath);
        _byPath = byPath;
    }

    /// <summary>No positions: every path locates at the start of the file.</summary>
    public static SourcePositions None { get; } = new(new Dictionary<string, SourcePosition>(StringComparer.Ordinal));

    /// <summary>
    /// The position of <paramref name="path"/>, or of its nearest recorded ancestor when the node isn't in the file
    /// (a missing field is reported where its parent starts), or the start of the file.
    /// </summary>
    /// <param name="path">A node path.</param>
    /// <returns>Its position.</returns>
    public SourcePosition Locate(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        for (var current = path; ; current = Parent(current))
        {
            if (_byPath.TryGetValue(current, out var position))
            {
                return position;
            }

            if (current.Length == 0)
            {
                return SourcePosition.Start;
            }
        }
    }

    private static string Parent(string path)
    {
        var cut = path.LastIndexOfAny(['.', '[']);
        return cut < 0 ? string.Empty : path[..cut];
    }
}
