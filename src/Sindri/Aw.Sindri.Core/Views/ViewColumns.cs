namespace Aw.Sindri.Core.Views;

/// <summary>Which columns each view indexes and where the value sits in the row's JSON (the one map the views.db migration and the in-memory store both follow).</summary>
public static class ViewColumns
{
    /// <summary>The JSON property path of <paramref name="column"/> in a row of <paramref name="view"/>, or null when the view doesn't index it.</summary>
    /// <param name="view">A <see cref="ViewNames"/> name.</param>
    /// <param name="column">The column.</param>
    /// <returns>The camel-case property names from the row's root, or null.</returns>
    public static IReadOnlyList<string>? JsonPath(string view, ViewColumn column)
    {
        ArgumentNullException.ThrowIfNull(view);
        var name = column switch
        {
            ViewColumn.Item => "item",
            ViewColumn.State => "state",
            ViewColumn.Repo => "repo",
            ViewColumn.Kind => "kind",
            _ => null,
        };
        return name is null
            ? null
            : view switch
            {
                ViewNames.Requests or ViewNames.Decisions when column != ViewColumn.Kind => [name],
                ViewNames.Jobs when column != ViewColumn.Item => [name],
                ViewNames.Launches when column != ViewColumn.Kind => [name],
                ViewNames.NeedsYou when column != ViewColumn.State => ["row", name],
                _ => null,
            };
    }
}
