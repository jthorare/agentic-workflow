namespace Aw.Sindri.Ui.ViewModels.Home;

/// <summary>One tab of the Home page: a saved filter over the row kinds (home spec §7.1).</summary>
internal enum HomeSplit
{
    /// <summary>Every kind.</summary>
    All,

    /// <summary>Reviews asked of the owner and PRs ready to merge.</summary>
    Reviews,

    /// <summary>Mentions, direct messages, thread replies and keywords.</summary>
    Messages,

    /// <summary>Issues, due todos, failed runs, decisions and questions.</summary>
    Work,

    /// <summary>Sessions that ask, models at their limit and lanes to recycle.</summary>
    Agents,
}
