namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The fixed words the checklist and Home's line bind (<see cref="SetupViewModel"/>, continued): each is the app's own copy in <see cref="Labels"/>, a property because the view binds it.</summary>
internal sealed partial class SetupViewModel
{
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    /// <summary>Home's one line.</summary>
    public string HomeLineText => Labels.SetupHomeLine;

    /// <summary>Home's line as assistive technology reads it: its words first, then how far setup is.</summary>
    public string HomeLineName => Labels.SetupHomeLine + ", " + CountText + (HomeStepText.Length > 0 ? ". " + Labels.SetupNext(HomeStepText) : string.Empty);

    /// <summary>The next-step control as assistive technology reads it: the step it opens.</summary>
    public string ContinueName => ContinueText;

    /// <summary>The one line under the lead.</summary>
    public string SubText => Labels.SetupSubline;

    /// <summary>The checklist before its first answer.</summary>
    public string ReadingText => Labels.ReadingSetup;

    /// <summary>The stale note's words.</summary>
    public string StaleText => Labels.Stale;

    /// <summary>The disclosure that holds the ids and codes.</summary>
    public string DetailsText => Labels.SetupDetails;

    /// <summary>The retry action's words.</summary>
    public string RetryText => Labels.TryAgain;

    /// <summary>"Sindri isn't running".</summary>
    public string NotRunningText => Labels.SindriNotRunning;

    /// <summary>The line over the launcher command.</summary>
    public string StartItText => Labels.SetupStartIt;

    /// <summary>What starting Sindri does for the app.</summary>
    public string ReconnectsText => Labels.SetupReconnects;

    /// <summary>What the checklist says when this Sindri has no setup list.</summary>
    public string NotServedText => Labels.SetupNotServed;

    /// <summary>What the checklist says when the list can't be read.</summary>
    public string UnreadableText => Labels.SetupUnreadable;
#pragma warning restore CA1822, MA0041
}
