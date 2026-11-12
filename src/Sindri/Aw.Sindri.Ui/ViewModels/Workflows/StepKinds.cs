#pragma warning disable MA0041, MA0182 // MA0041: stub bodies; MA0182: wired into the page views in the next round

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One name per step kind (#3074): the catalog's title wherever the kind is shown. A type id such as "condition" shows only in the JSON view.</summary>
internal static class StepKinds
{
    /// <summary>The block types the app knows a name for when the catalog is not loaded, with that name (the titles on main).</summary>
    internal static IReadOnlyDictionary<string, string> Fallback => throw new NotSupportedException();

    /// <summary>The name of a kind.</summary>
    /// <param name="type">The block type id.</param>
    /// <param name="catalogTitle">The catalog's title for it, or null when the catalog has none or is not loaded.</param>
    /// <returns>The catalog title; else the known name; else the type id as words ("my_block" gives "My block").</returns>
    internal static string Name(string type, string? catalogTitle = null) => throw new NotSupportedException();
}
