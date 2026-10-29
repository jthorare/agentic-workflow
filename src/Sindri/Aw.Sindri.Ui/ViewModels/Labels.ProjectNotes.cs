using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the notes area of a Projects card (projects page spec §3, §5.1). Never a served sentence.</summary>
internal static partial class Labels
{
    /// <summary>The most characters the API takes in a project's notes.</summary>
    internal const int ProjectNotesMax = 16_384;

    /// <summary>The button that opens the notes for editing.</summary>
    internal const string ProjectNotesEdit = "Edit notes";

    /// <summary>The button that sends the notes.</summary>
    internal const string ProjectNotesSave = "Save";

    /// <summary>The Save button while the write is on its way.</summary>
    internal const string ProjectNotesSaving = "Saving…";

    /// <summary>The button that drops the edit.</summary>
    internal const string ProjectNotesCancel = "Cancel";

    /// <summary>The button that re-reads the card after a conflict.</summary>
    internal const string ProjectNotesReload = "Reload";

    /// <summary>The line under the box when the project changed since it was opened (SND-PROJECT-409).</summary>
    internal const string ProjectNotesConflict = "Changed elsewhere since you opened it.";

    /// <summary>The live region's resting text for a card's notes.</summary>
    internal const string ProjectNotesResting = "Project notes";

    /// <summary>The box's accessible name.</summary>
    internal const string ProjectNotesBoxName = "Notes";

    /// <summary>The counter under the box: "1,204 / 16,384".</summary>
    /// <param name="length">How many characters are typed.</param>
    /// <returns>The words.</returns>
    internal static string ProjectNotesCounter(int length) =>
        string.Create(CultureInfo.InvariantCulture, $"{length:N0} / {ProjectNotesMax:N0}");

    /// <summary>What a screen reader hears once the notes are saved.</summary>
    /// <param name="name">The project's name.</param>
    /// <returns>The words.</returns>
    internal static string ProjectNotesSaved(string name) => "Notes saved for " + name;

    /// <summary>The line under Save when the write was refused.</summary>
    /// <param name="error">The API's refusal.</param>
    /// <returns>The words.</returns>
    internal static string ProjectNotesRefused(Kernel.Error error) => InstanceRefused(error);
}
