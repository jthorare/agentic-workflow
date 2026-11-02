namespace Aw.Context.Core.Extract.DataStores;

/// <summary>One use of a data store a file's text shows.</summary>
/// <param name="store">The store's name as the adapter reads it from the text.</param>
/// <param name="access">Whether the file reads or writes it.</param>
/// <param name="match">The text that showed it.</param>
public sealed class StoreAccess(string store, DataAccess access, string match)
{
    /// <summary>The store's name.</summary>
    public string Store { get; } = store;

    /// <summary>Whether the file reads or writes the store.</summary>
    public DataAccess Access { get; } = access;

    /// <summary>The text that showed the use.</summary>
    public string Match { get; } = match;
}
