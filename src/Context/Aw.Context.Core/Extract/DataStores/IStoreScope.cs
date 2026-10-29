namespace Aw.Context.Core.Extract.DataStores;

/// <summary>What the names in one code file stand for, when they stand for a declared store.</summary>
internal interface IStoreScope
{
    /// <summary>The store a name used in the file stands for: an import (named or renamed) of a declared identifier.</summary>
    /// <param name="local">The name as the file uses it.</param>
    /// <returns>The store's name, or null when the name is not a declared store.</returns>
    public string? Store(string local);

    /// <summary>The store 'ns.member' stands for, where 'ns' is a namespace import.</summary>
    /// <param name="ns">The namespace name as the file uses it.</param>
    /// <param name="member">The member name.</param>
    /// <returns>The store's name, or null.</returns>
    public string? Member(string ns, string member);

    /// <summary>The store a schema key names ('db.query.posts': the key is an exported identifier of the schema, not an import of the file).</summary>
    /// <param name="key">The key.</param>
    /// <returns>The store's name when exactly one declared identifier has that name, else null.</returns>
    public string? Key(string key);
}
