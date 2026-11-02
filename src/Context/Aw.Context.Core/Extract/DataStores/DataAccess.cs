namespace Aw.Context.Core.Extract.DataStores;

/// <summary>How a code file uses a data store.</summary>
public enum DataAccess
{
    /// <summary>It reads rows or documents from the store.</summary>
    Read,

    /// <summary>It inserts, updates or deletes in the store.</summary>
    Write,
}
