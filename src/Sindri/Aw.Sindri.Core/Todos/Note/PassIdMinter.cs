
namespace Aw.Sindri.Core.Todos.Note;

/// <summary>Mints the ids of the lines one sync pass adopts, once per line, and gives the same id back when a re-write inside the pass asks again (todo spec §4.1: the pass keys a line by its text and index).</summary>
/// <param name="source">Makes a new id ('td_' plus a ULID); injected, so Core reads no clock and no random.</param>
public sealed class PassIdMinter(Func<TodoId> source)
{
    private readonly Dictionary<string, TodoId> _minted = new(StringComparer.Ordinal);

    /// <summary>Makes a new id.</summary>
    public Func<TodoId> Source { get; } = source;

    /// <summary>The id for a line.</summary>
    /// <param name="key">The line's key, its index and text.</param>
    /// <returns>A new id the first time, the same one after.</returns>
    public TodoId Mint(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_minted.TryGetValue(key, out var id))
        {
            id = Source();
            _minted[key] = id;
        }

        return id;
    }
}
