namespace Aw.Sindri.Core.Views;

/// <summary>
/// A column a view can be filtered by (a fixed set, like <see cref="ViewNames"/>): the request, decision and needs-you views index
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
}
