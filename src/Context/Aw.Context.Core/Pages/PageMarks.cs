namespace Aw.Context.Core.Pages;

/// <summary>Which pages a read keeps: the owner's named pages, the suggested ones (surfaces the map offers that no profile page names), or both.</summary>
public enum PageMarks
{
    /// <summary>Owner and suggested pages.</summary>
    All,

    /// <summary>Only the pages the profile's 'index.pages' names.</summary>
    Owner,

    /// <summary>Only the pages the map offers that the profile does not name.</summary>
    Suggested,
}
