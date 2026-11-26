using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the step editor's notes and field lines, and the notes on a run's step cards and Run now (#3682). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The editor's command that reads the workflow again after it changed elsewhere.</summary>
    internal const string WfReload = "Reload";

    /// <summary>The editor's prompt when the workflow changed elsewhere: the edit was not saved and nothing is retried.</summary>
    internal const string WfEditChangedElsewhere = "This workflow changed elsewhere, so your edit was not saved. Reload to see the new version, then make the edit again.";

    /// <summary>The note on the run Run now opened instead of starting a second one.</summary>
    internal const string WfRunInFlight = "A run is already in flight.";

    /// <summary>Under a notification step's title and message on the run page: where it went.</summary>
    internal const string WfNotifySentTo = "Sent to the Sindri inbox";

    /// <summary>The button on a failed step's card on the run page.</summary>
    internal const string WfEditStep = "Edit step";

    /// <summary>The editor's note when an edit was refused or failed: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditRefused(Error error) => "Couldn't save the edit. " + InstanceRefused(error);

    /// <summary>The editor's note when Sindri did not answer an edit: it may have been saved.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditUnsure(Error error) => "Sindri didn't answer, so the edit may have been saved. Reload to check before you edit again. " + InstanceRefused(error);

    /// <summary>The editor's note when the workflow or the block list can't be read: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditLoadFailed(Error error) => "Couldn't read the workflow to edit. " + InstanceRefused(error);

    /// <summary>The editor's note when an edit was saved but the workflow can't be read again: the reason follows.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfEditSavedReloadFailed(Error error) => "The edit was saved, but the workflow couldn't be read again. Reload to see it. " + InstanceRefused(error);

    /// <summary>The line under a required field left empty.</summary>
    internal const string WfFieldNeeded = "This field is required.";

    /// <summary>The line under a number field that isn't a number.</summary>
    internal const string WfFieldNotNumber = "Enter a number.";

    /// <summary>The line under a boolean field that isn't true or false.</summary>
    internal const string WfFieldNotBoolean = "Enter true or false.";

    /// <summary>The line under an object field that isn't a JSON object.</summary>
    internal const string WfFieldNotObject = "Enter a JSON object, such as {\"key\": \"value\"}.";
}
