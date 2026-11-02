namespace Aw.Context.Core.Extract.DataStores;

/// <summary>An identifier a schema file declares for a store.</summary>
/// <param name="Identifier">The exported name in the code.</param>
/// <param name="Store">The store's own name, as <see cref="IDataStoreAdapter.Declared"/> gives it.</param>
internal sealed record StoreBinding(string Identifier, string Store);
