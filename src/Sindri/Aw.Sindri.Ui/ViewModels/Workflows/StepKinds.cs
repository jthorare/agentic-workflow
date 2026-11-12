using System.Collections.Frozen;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One name per step kind (#3074): the catalog's title wherever the kind is shown. A type id such as "condition" shows only in the JSON view.</summary>
internal static class StepKinds
{
    /// <summary>The block types the app knows a name for when the catalog is not loaded, with that name (the titles on main).</summary>
    internal static IReadOnlyDictionary<string, string> Fallback { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["manual"] = "When called",
        ["schedule"] = "Schedule",
        ["connection_event"] = "Connection event",
        ["only_new"] = "Only new",
        ["http"] = "HTTP request",
        ["prompt"] = "Prompt",
        ["run_agent"] = "Run agent",
        ["condition"] = "If",
        ["for_each"] = "For each",
        ["wait"] = "Wait",
        ["code"] = "Code",
        ["call_tool"] = "Toolkit tool",
        ["run_workflow"] = "Run workflow",
        ["workflow_result"] = "Workflow result",
        ["create_item"] = "Create item",
        ["notify"] = "Send notification",
        ["ask_owner"] = "Ask owner",
        ["memory_search"] = "Recall",
        ["memory_write"] = "Remember",
        ["memory_forget"] = "Forget",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The name of a kind.</summary>
    /// <param name="type">The block type id.</param>
    /// <param name="catalogTitle">The catalog's title for it, or null when the catalog has none or is not loaded.</param>
    /// <returns>The catalog title; else the known name; else the type id as words ("my_block" gives "My block").</returns>
    internal static string Name(string type, string? catalogTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(catalogTitle))
        {
            return catalogTitle;
        }

        if (Fallback.TryGetValue(type, out var known))
        {
            return known;
        }

        var words = type.Replace('_', ' ').Trim();
        return words.Length == 0 ? type : char.ToUpperInvariant(words[0]) + words[1..];
    }
}
