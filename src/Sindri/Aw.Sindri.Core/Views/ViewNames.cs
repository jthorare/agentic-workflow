namespace Aw.Sindri.Core.Views;

/// <summary>The view tables (a fixed set: an adapter never builds a table name from input).</summary>
public static class ViewNames
{
    /// <summary>Each service's last run.</summary>
    public const string Services = "services";

    /// <summary>The approved and live profile.</summary>
    public const string Profile = "profile";

    /// <summary>Work items, keyed by item id.</summary>
    public const string Items = "items";

    /// <summary>Tracker aliases, keyed by alias: the item each one names.</summary>
    public const string ItemAliases = "item-aliases";

    /// <summary>Each item's timeline, keyed '&lt;itemId&gt;/&lt;sequence:D19&gt;'.</summary>
    public const string ItemTimeline = "item-timeline";

    /// <summary>Filed requests, keyed by request id (API P4).</summary>
    public const string Requests = "requests";

    /// <summary>Decisions, keyed by decision id (API P4).</summary>
    public const string Decisions = "decisions";

    /// <summary>The human's list, keyed by the row's id (API P4).</summary>
    public const string NeedsYou = "needs_you";

    /// <summary>Jobs, keyed by job id (API P6).</summary>
    public const string Jobs = "jobs";

    /// <summary>Launches, keyed by launch id (API P10a).</summary>
    public const string Launches = "launches";

    /// <summary>The newest built code index per repo (code index spec §8), folded from 'codeindex.built'.</summary>
    public const string CodeIndex = "code-index";

    /// <summary>The recipe stats cache, keyed '&lt;recipe&gt;/&lt;window&gt;/&lt;hash or -&gt;' (API P12): a read-through the stats reads fill and a run's start or end empties.</summary>
    public const string RecipeStats = "recipe-stats";

    /// <summary>How each model has done at each state of each recipe version (model-pick spec §7.2), keyed by ModelHistoryKeys.For.</summary>
    public const string ModelHistory = "model-history";

    /// <summary>What the model-history fold remembers to join a run to its model and recipe: registered hashes, started instances, launches and completed runs.</summary>
    public const string ModelRuns = "model-runs";

    /// <summary>Attached and hand-made projects, keyed by project id (projects page spec §5.2).</summary>
    public const string ProjectAttached = "project-attached";

    /// <summary>Whether a led Linear project is hidden, keyed by its Linear key (projects page spec §5.2).</summary>
    public const string ProjectHidden = "project-hidden";

    /// <summary>Every view that has a table.</summary>
    public static IReadOnlyList<string> All { get; } = [CodeIndex, Services, Profile, Items, ItemAliases, ItemTimeline, Requests, Decisions, NeedsYou, Jobs, Launches, RecipeStats, ModelHistory, ModelRuns, ProjectAttached, ProjectHidden];
}
