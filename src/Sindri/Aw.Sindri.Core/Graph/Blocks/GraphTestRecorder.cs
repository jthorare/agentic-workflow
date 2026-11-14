namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The "would have" rows of one test-run block execution (spec 2026-11-12 §5.1). The engine opens a recorder around a block in a test run; a fenced port
/// (<see cref="GraphFence"/>) that sees an open recorder records what it would have done and does nothing else. With none open, a fenced port runs for real.
/// </summary>
public static class GraphTestRecorder
{
    private static readonly AsyncLocal<List<string>?> _open = new();

    /// <summary>True while a recorder is open on this call chain: the block runs in a test run.</summary>
    public static bool Active => _open.Value is not null;

    /// <summary>Opens a recorder for the block execution that follows.</summary>
    /// <returns>The list the rows are added to, in order.</returns>
    public static IList<string> Begin()
    {
        var lines = new List<string>();
        _open.Value = lines;
        return lines;
    }

    /// <summary>Closes the recorder of this call chain.</summary>
    public static void End() => _open.Value = null;

    /// <summary>Adds a row to the open recorder.</summary>
    /// <param name="line">What a live run would have done, such as "would have: notify 'Main gate red'".</param>
    public static void Record(string line) => _open.Value?.Add(line);
}
