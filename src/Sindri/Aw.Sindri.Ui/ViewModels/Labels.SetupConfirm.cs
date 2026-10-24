using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the confirmations in the setup rows (F3 setup spec §2.1, §2.2). No em dashes, no internal terms.</summary>
internal static partial class Labels
{
    /// <summary>Said when the settings changed while the person was reading them: the phrase was tied to the old list, so a new one is asked for.</summary>
    internal const string SetupProfileChangedWhileReading = "This changed while you were reading it. Read it again, then approve.";

    /// <summary>Said while the settings the approval is tied to are not known yet.</summary>
    internal const string SetupProfileReading = "Sindri is reading your settings.";

    /// <summary>The line under the approve-all hold and its typed alternative: what a hold or the phrase does.</summary>
    internal const string SetupWorkflowsEvery = "Every workflow passed all of its own checks when Sindri was built.";

    /// <summary>The heading over the approval summary.</summary>
    internal const string ProfileSummaryTitle = "What approving lets Sindri do";

    /// <summary>The heading over the services the profile newly runs.</summary>
    internal const string ProfileSummaryServices = "Services";

    /// <summary>The heading over the effects and limits the profile newly allows.</summary>
    internal const string ProfileSummaryAllowance = "What workflows may do";

    /// <summary>The heading over the outside systems the profile adds or changes.</summary>
    internal const string ProfileSummaryConnections = "Outside systems";

    /// <summary>The heading over what Sindri keeps for the person.</summary>
    internal const string ProfileSummaryHumanOnly = "Waits for your answer";

    /// <summary>A repo's path and size under its name: "/work/demo, 1,204 files, 38.2 MB".</summary>
    /// <param name="path">The real path Sindri would read.</param>
    /// <param name="files">The tracked file count.</param>
    /// <param name="megabytes">The tracked size in MiB.</param>
    /// <returns>The line.</returns>
    internal static string ProfileSummaryRepoLine(string path, long files, double megabytes) =>
        string.Create(CultureInfo.InvariantCulture, $"{path}, {files:N0} {(files == 1 ? "file" : "files")}, {megabytes:N1} MB");
}
