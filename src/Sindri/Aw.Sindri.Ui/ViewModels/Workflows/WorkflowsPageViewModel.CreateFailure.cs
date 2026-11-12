using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): a failed create as the shared line (#3066).</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>The refusal or the unsure answer as the shared failed line draws it (the sentence, Try again, the code behind Details); null for a taken short id, which the person fixes in the form.</summary>
    public FailedRead? CreateFailure { get; private set => Set(ref field, value); }

    /// <summary>True when the plain red sentence shows: a taken short id.</summary>
    public bool ShowsPlainCreateError => HasCreateError && CreateFailure is null;
}
