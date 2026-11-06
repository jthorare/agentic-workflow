namespace Aw.Context.Core.Library;

/// <summary>The design-doc error codes (spec 2026-11-04 §4.2, Errors).</summary>
public static class DesignDocCodes
{
    /// <summary>A missing or bad front-matter key.</summary>
    public const string BadFrontMatter = "SND-LIB-010";

    /// <summary>An item id of a bad shape.</summary>
    public const string BadItemId = "SND-LIB-011";

    /// <summary>A doc id another doc in the scope also has (decided on read, by <see cref="DesignDocScope"/>).</summary>
    public const string SharedDocId = "SND-LIB-012";

    /// <summary>Two headings, or two list items, defining one item id.</summary>
    public const string DuplicateItem = "SND-LIB-013";

    /// <summary>An acceptance criterion no row pairs and no requirement precedes in its section.</summary>
    public const string OrphanAcceptance = "SND-LIB-014";

    /// <summary>A ref to an item the scope doesn't have (decided on read).</summary>
    public const string BrokenRef = "SND-LIB-015";

    /// <summary>An unknown <c language="text">parent</c> or <c language="text">supersedes</c> (decided on read).</summary>
    public const string UnknownDocLink = "SND-LIB-016";

    /// <summary>A hand-edited generated file.</summary>
    public const string StaleGenerated = "SND-LIB-017";

    /// <summary>Too large (over 1 MiB or 2,000 items).</summary>
    public const string TooLarge = "SND-LIB-018";

    /// <summary>A near miss: <c language="text">aw-doc:</c> present but not a whole number (<c language="text">"1"</c>, <c language="text">1.0</c>, <c language="text">yes</c>); a whole number other than 1 is <see cref="BadFrontMatter"/>.</summary>
    public const string NearMiss = "SND-LIB-019";

    /// <summary>The codes decided against the scope when read: a doc that has only these keeps its items.</summary>
    /// <param name="code">A code.</param>
    /// <returns>True for 012, 015 and 016.</returns>
    public static bool IsScopeCode(string code) => code switch
    {
        SharedDocId or BrokenRef or UnknownDocLink => true,
        _ => false,
    };
}
