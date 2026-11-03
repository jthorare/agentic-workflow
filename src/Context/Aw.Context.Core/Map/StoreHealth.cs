namespace Aw.Context.Core.Map;

/// <summary>Doctor 'context:store': warns while the write-ahead log is over 1 GiB and larger than the database itself.</summary>
public static class StoreHealth
{
    /// <summary>Judges the store's file sizes.</summary>
    /// <param name="sizes">The sizes, null when there is no store file yet.</param>
    /// <returns>The row.</returns>
    public static MapHealth Check(StoreFileSizes? sizes) => throw new NotSupportedException();
}
