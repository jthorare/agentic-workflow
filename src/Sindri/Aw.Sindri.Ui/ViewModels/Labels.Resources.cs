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
}
