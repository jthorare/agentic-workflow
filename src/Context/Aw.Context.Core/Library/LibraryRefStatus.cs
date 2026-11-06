namespace Aw.Context.Core.Library;

/// <summary>How a ref written in a PR body resolved against the docs the caller may read.</summary>
public enum LibraryRefStatus
{
    /// <summary>It names a doc and an item the library holds.</summary>
    Resolved,

    /// <summary>No readable doc, or no such item in it: a broken claim (a claim written before its doc lands is this until the doc does).</summary>
    Missing,

    /// <summary>Two docs match at the same step (SND-LIB-022): use the full ref.</summary>
    Ambiguous,
}
