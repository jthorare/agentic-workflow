using System.Globalization;
using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of a workflow's Delete (Workflows page spec §4.9, #3306): the ⋯ menu item, the confirm's three forms and the notice after.</summary>
internal static partial class Labels
{
    /// <summary>The header's ⋯ menu button, named for screen readers.</summary>
    internal const string WfMore = "More actions";

    /// <summary>The ⋯ menu's last item: opens the delete confirm. It never deletes on its own.</summary>
    internal const string WfDelete = "Delete…";

    /// <summary>The confirm's red button on an off workflow with no run going.</summary>
    internal const string WfDeleteYes = "Delete";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfDeleteBusy = "Deleting…";

    /// <summary>The confirm's button that closes it and changes nothing. Focus starts on it.</summary>
    internal const string WfDeleteCancel = "Cancel";

    /// <summary>The active form's button: pauses the workflow, then the confirm shows its first form in place.</summary>
    internal const string WfDeletePause = "Pause";

    /// <summary>The in-flight form's button: opens the run that is still going.</summary>
    internal const string WfDeleteOpenRun = "Open the run";

    /// <summary>The off form's footer: the two keys.</summary>
    internal const string WfDeleteKeys = "Ctrl+Enter deletes · Esc closes";

    /// <summary>The confirm on an off workflow with no run going.</summary>
    /// <param name="name">The workflow's name.</param>
    /// <returns>The words.</returns>
    internal static string WfDeleteConfirm(string name) =>
        string.Create(CultureInfo.InvariantCulture, $"Delete {name}? Its runs stay readable on the Runs page. Its resources are deleted. You can't undo this.");

    /// <summary>The confirm on an active workflow (SND-GRAPH-114): it must be paused first.</summary>
    /// <param name="name">The workflow's name.</param>
    /// <returns>The words.</returns>
    internal static string WfDeleteActive(string name) => string.Create(CultureInfo.InvariantCulture, $"{name} is active. Pause it, then delete it.");

    /// <summary>The confirm while a run is going (SND-GRAPH-115).</summary>
    /// <param name="name">The workflow's name.</param>
    /// <returns>The words.</returns>
    internal static string WfDeleteInFlight(string name) => string.Create(CultureInfo.InvariantCulture, $"A run of {name} is still going. Delete it after the run ends.");

    /// <summary>The notice after a delete: "Deleted nightly-size. 12 runs kept, 1.2 MB freed" (1 MB is 1,000,000 bytes), then the owners whose resources stayed.</summary>
    /// <param name="slug">The workflow's slug, as the server answered.</param>
    /// <param name="runsKept">The runs that stay readable.</param>
    /// <param name="bytesFreed">The bytes the deleted resources held.</param>
    /// <param name="stayed">The owners whose resources could not be deleted.</param>
    /// <returns>The words.</returns>
    internal static string WfDeleted(string slug, int runsKept, long bytesFreed, IReadOnlyList<string> stayed)
    {
        var runs = runsKept == 1 ? "1 run" : runsKept.ToString(CultureInfo.InvariantCulture) + " runs";
        var line = string.Create(CultureInfo.InvariantCulture, $"Deleted {slug}. {runs} kept, {bytesFreed / 1_000_000.0:0.0} MB freed");
        return stayed.Count == 0 ? line : line + ". These resources stayed: " + string.Join(", ", stayed) + "; run 'sindri resources purge <owner>' to try again";
    }

    /// <summary>The note in the confirm when Delete was refused for another reason or failed.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfDeleteRefused(Error error) => "Couldn't delete the workflow. " + InstanceRefused(error);
}
