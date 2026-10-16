using Temporalio.Api.Enums.V1;
using Temporalio.Common;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The fixed names of Sindri's Temporal deployment (engine spec rev 8 §0 "Operations"); the namespace and task queue are <see cref="TemporalSettings"/>.</summary>
internal static class SindriTemporal
{
    /// <summary>The dev server's SQLite file, directly under the Sindri home.</summary>
    public const string DatabaseFile = "temporal.db";

    /// <summary>The dev server's output, under &lt;home&gt;/logs (appended, 0600).</summary>
    public const string LogFile = "temporal.log";

    /// <summary>The 'temporal' CLI version binaries.lock pins (a test keeps the two equal).</summary>
    public const string PinnedVersion = "1.9.1";

    /// <summary>The visibility search attributes every instance sets (engine spec rev 8 §5.1), registered on every start.</summary>
    public static IReadOnlyList<(string Name, IndexedValueType Type)> SearchAttributes { get; } =
    [
        ("Item", IndexedValueType.Keyword),
        ("Recipe", IndexedValueType.Keyword),
        ("RecipeHash", IndexedValueType.Keyword),
        ("State", IndexedValueType.Keyword),
        ("NeedsYou", IndexedValueType.Bool),
        ("Repo", IndexedValueType.Keyword),
        ("Tenant", IndexedValueType.Keyword),
        ("Ring", IndexedValueType.Keyword),
        ("Urgency", IndexedValueType.Keyword),
        ("Subject", IndexedValueType.Keyword),
        ("Root", IndexedValueType.Bool),
    ];

    /// <summary>The 'Item' search attribute: the work item.</summary>
    public static SearchAttributeKey<string> ItemKey { get; } = SearchAttributeKey.CreateKeyword("Item");

    /// <summary>The 'Recipe' search attribute: the recipe id.</summary>
    public static SearchAttributeKey<string> RecipeKey { get; } = SearchAttributeKey.CreateKeyword("Recipe");

    /// <summary>The 'RecipeHash' search attribute: the hash of the recipe version the instance runs, so a registry wake finds its instances (engine P6 integration).</summary>
    public static SearchAttributeKey<string> RecipeHashKey { get; } = SearchAttributeKey.CreateKeyword("RecipeHash");

    /// <summary>The 'State' search attribute: the state the instance is in.</summary>
    public static SearchAttributeKey<string> StateKey { get; } = SearchAttributeKey.CreateKeyword("State");

    /// <summary>The 'NeedsYou' search attribute: a desk run waits on a human.</summary>
    public static SearchAttributeKey<bool> NeedsYouKey { get; } = SearchAttributeKey.CreateBool("NeedsYou");

    /// <summary>The 'Repo' search attribute: the repo 'fact:item' names; unset on an instance without one.</summary>
    public static SearchAttributeKey<string> RepoKey { get; } = SearchAttributeKey.CreateKeyword("Repo");

    /// <summary>The 'Tenant' search attribute: the instance's tenant key.</summary>
    public static SearchAttributeKey<string> TenantKey { get; } = SearchAttributeKey.CreateKeyword("Tenant");

    /// <summary>The 'Ring' search attribute: the instance's ring, as text.</summary>
    public static SearchAttributeKey<string> RingKey { get; } = SearchAttributeKey.CreateKeyword("Ring");

    /// <summary>The 'Urgency' search attribute: the recipe's urgency, 'incident', 'triage' or 'work'.</summary>
    public static SearchAttributeKey<string> UrgencyKey { get; } = SearchAttributeKey.CreateKeyword("Urgency");

    /// <summary>The 'Subject' search attribute: the key of the recipe's first declared input fact; unset when it declares none.</summary>
    public static SearchAttributeKey<string> SubjectKey { get; } = SearchAttributeKey.CreateKeyword("Subject");

    /// <summary>The 'Root' search attribute: true for a top-level instance, false for a child.</summary>
    public static SearchAttributeKey<bool> RootKey { get; } = SearchAttributeKey.CreateBool("Root");
}
