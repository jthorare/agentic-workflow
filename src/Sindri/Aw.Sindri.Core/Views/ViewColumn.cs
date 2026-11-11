namespace Aw.Sindri.Core.Views;

/// <summary>
/// A column a view can be filtered by (a fixed set, like <see cref="ViewNames"/>): the request, decision, needs-you and jobs views index
/// the fields their projections and queries look rows up by.
/// </summary>
public enum ViewColumn
{
    /// <summary>The item id the row concerns.</summary>
    Item,

    /// <summary>The row's state, in its wire name ('pending', 'open').</summary>
    State,

    /// <summary>The repo the row belongs to.</summary>
    Repo,

    /// <summary>The row's kind, in its wire name ('failure').</summary>
    Kind,

    /// <summary>How a launch's session ended, in its wire name ('idle'); the launches view only (issue #2786).</summary>
    Reason,

    /// <summary>The workflow instance address of a launch's Step; the launches view only (issue #2786).</summary>
    Instance,
}
