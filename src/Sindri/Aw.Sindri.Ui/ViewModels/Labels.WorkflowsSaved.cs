using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): a saved revision that is not the live one on the Workflows page (#3303). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>Run now's words while a newer revision is saved but not live: it runs the live one.</summary>
    /// <param name="revision">The live revision.</param>
    /// <returns>The words.</returns>
    internal static string WfRunNowLive(int revision) => string.Create(CultureInfo.InvariantCulture, $"Run revision {revision} (live)");

    /// <summary>The line after a save on a workflow that is on: the new revision is saved, triggers still run the old one.</summary>
    /// <param name="revision">The saved revision.</param>
    /// <returns>The words.</returns>
    internal static string WfSavedNotLive(int revision) => string.Create(CultureInfo.InvariantCulture, $"Saved as revision {revision}. Not live yet.");

    /// <summary>The line after a save on a workflow that is off.</summary>
    /// <param name="revision">The saved revision.</param>
    /// <returns>The words.</returns>
    internal static string WfSaved(int revision) => string.Create(CultureInfo.InvariantCulture, $"Saved as revision {revision}.");

    /// <summary>The button that turns the newest saved revision on.</summary>
    internal const string WfMakeLive = "Make live";

    /// <summary>The same button while its call is on the way.</summary>
    internal const string WfMakeLiveBusy = "Making live…";
}
