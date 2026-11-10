using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Resources page (PR 18b). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The page's name (it is not a tab: the palette and System's link open it).</summary>
    internal const string ResourcesPage = "Resources";

    /// <summary>The page before its first answer.</summary>
    internal const string ReadingResources = "Reading resources…";

    /// <summary>The page with no resource.</summary>
    internal const string ResourcesEmpty = "No resources. Nothing holds a database or a folder right now.";

    /// <summary>The Purge action.</summary>
    internal const string ResourcePurge = "Purge";

    /// <summary>The confirm button.</summary>
    internal const string ResourcePurgeConfirm = "Purge now";

    /// <summary>The button that backs out of the question.</summary>
    internal const string ResourcePurgeKeep = "Keep";

    /// <summary>The confirm's question, naming the owner.</summary>
    /// <param name="owner">The owner.</param>
    /// <returns>The words.</returns>
    internal static string ResourcePurgeAsk(string owner) => $"Purge every resource of {owner}? This deletes their data.";

    /// <summary>A purge the API refused or that failed: the reason follows. The rows stay.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string ResourcePurgeFailed(Error error) => "Couldn't purge. " + InstanceRefused(error);

    /// <summary>A purge that worked: "Purged 2 resources of workflow:triage."</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="removed">How many were removed.</param>
    /// <returns>The words.</returns>
    internal static string ResourcePurged(string owner, int removed) =>
        string.Create(CultureInfo.InvariantCulture, $"Purged {removed} {(removed == 1 ? "resource" : "resources")} of {owner}.");

    /// <summary>A size against its cap: "1.2 MB of 5 MB".</summary>
    /// <param name="size">The size in words.</param>
    /// <param name="limit">The limit in words.</param>
    /// <returns>The words.</returns>
    internal static string ResourceSizeOf(string size, string limit) => size + " of " + limit;

    /// <summary>The page's one-line purpose.</summary>
    internal const string ResourcesPurpose = "What each owner holds: databases and folders, with their size. Purge deletes everything one owner holds, and is refused while that owner is active.";

    /// <summary>System's link to the Resources page.</summary>
    internal const string ResourcesLink = "Resources";

    /// <summary>The link's accessible name: its visible words first.</summary>
    internal const string ResourcesLinkName = "Resources, opens the Resources page";

    /// <summary>The head: "3 resources".</summary>
    /// <param name="count">How many resources.</param>
    /// <returns>The words.</returns>
    internal static string ResourcesHead(int count) => string.Create(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "resource" : "resources")}");

    /// <summary>A read that failed: the reason follows. The rows stay.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string ResourcesFailed(Error error) => "Couldn't read the resources just now. " + InstanceRefused(error);

    /// <summary>An action's accessible name: "Purge workflow:triage".</summary>
    /// <param name="action">The action's visible words.</param>
    /// <param name="owner">The owner.</param>
    /// <returns>The words.</returns>
    internal static string ResourceActionName(string action, string owner) => action + " " + owner;
}
