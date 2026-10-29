namespace Aw.Context.Core.Extract.DataStores;

/// <summary>
/// An adapter for a query library whose code names a store by a program identifier, not by the store's own name (Drizzle: 'export const posts = pgTable('blog_posts', …)', then
/// 'db.select().from(posts)'). It declares which identifier stands for which store, and reads a code file's uses through a scope that says what each name in the file stands for.
/// </summary>
internal interface IBindingDataStoreAdapter : IDataStoreAdapter
{
    /// <summary>The identifiers a schema file declares, each with the store it stands for.</summary>
    /// <param name="text">The text of a file that matches the schema globs.</param>
    /// <returns>The identifiers and their stores.</returns>
    public IEnumerable<StoreBinding> DeclaredBindings(string text);

    /// <summary>The uses of declared stores a code file shows.</summary>
    /// <param name="text">The text of a code file.</param>
    /// <param name="scope">What the names in that file stand for.</param>
    /// <returns>One entry per use found.</returns>
    public IEnumerable<StoreAccess> Accesses(string text, IStoreScope scope);
}
