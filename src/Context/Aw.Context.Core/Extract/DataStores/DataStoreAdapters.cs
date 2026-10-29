namespace Aw.Context.Core.Extract.DataStores;

/// <summary>The adapters the map build runs, like <see cref="Recognizers.All"/>: a new language or query library is a new row.</summary>
public static class DataStoreAdapters
{
    /// <summary>Every adapter, in order.</summary>
    public static IReadOnlyList<IDataStoreAdapter> All { get; } = [new GenericSqlAdapter(), new DrizzleAdapter()];
}
