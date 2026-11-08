namespace Aw.Sindri.Core.Todos;

/// <summary>The four state changes of a todo that take only its id and, optionally, its version (todo spec §4.4: done, reopen, remove, restore).</summary>
public enum TodoVerb
{
    /// <summary>Check it off.</summary>
    Done,

    /// <summary>Reopen a done todo.</summary>
    Reopen,

    /// <summary>Delete it (soft).</summary>
    Remove,

    /// <summary>Bring back a removed todo.</summary>
    Restore,
}
