using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The Start form's copy (U5'): the form's own words; a refusal is always the API's, never a sentence made here.</summary>
internal static partial class Labels
{
    /// <summary>The form's heading and its button: the catalog's label for StartInstance.</summary>
    internal static readonly string StartWorkflow = Action("StartInstance");

    /// <summary>The recipe picker's label.</summary>
    internal const string StartRecipe = "Workflow type";

    /// <summary>The item field's label.</summary>
    internal const string StartItem = "Item";

    /// <summary>The caption of the list of items, above it.</summary>
    internal const string StartPickCaption = "Or pick one from the queue";

    /// <summary>The list of items to fill the item field from.</summary>
    internal const string StartPickItem = "Pick an item";

    /// <summary>The line under an input that need not be given.</summary>
    internal const string GeneratedIfEmpty = "Filled in for you if you leave it empty";

    /// <summary>The file input's button.</summary>
    internal const string ChooseFile = "Choose file…";

    /// <summary>A file input before a file is chosen.</summary>
    internal const string NoFileChosen = "No file chosen";

    /// <summary>The picker's title.</summary>
    internal const string ChooseFileTitle = "Choose a file for the input";

    /// <summary>While the start is in flight.</summary>
    internal const string StartSending = "Starting…";

    /// <summary>The recipes are being read.</summary>
    internal const string StartLoading = "Reading the workflows…";

    /// <summary>The chosen recipe's inputs are being read.</summary>
    internal const string StartReadingInputs = "Reading what the workflow needs…";

    /// <summary>Why Start is off while the inputs are read.</summary>
    internal const string StartWhyReading = "Start waits until the workflow's inputs are read.";

    /// <summary>Why Start is off while the start is in flight.</summary>
    internal const string StartWhySending = "A start is being sent.";

    /// <summary>Why Start is off once the start was accepted for this item and recipe.</summary>
    internal const string StartWhySent = "This start was already accepted; choose another item or workflow to start again.";

    /// <summary>An instance a send opened while the form had moved on to something else.</summary>
    /// <param name="address">Its address.</param>
    /// <returns>The sentence.</returns>
    internal static string StartedBehind(string address) => $"{address} started.";

    /// <summary>The file button's name: the input's words, then what is chosen.</summary>
    /// <param name="caption">The input's caption.</param>
    /// <param name="file">The file line: the file chosen, or "No file chosen".</param>
    /// <returns>The name.</returns>
    internal static string StartFileButtonName(string caption, string file) => $"{ChooseFile.TrimEnd('…')}, {caption}, {file}";

    /// <summary>No recipe can be started.</summary>
    internal const string StartNoRecipes = "No approved workflow is available, so nothing can be started.";

    /// <summary>The button of a second attempt.</summary>
    internal const string StartNewAttempt = "Start a new attempt";

    /// <summary>The catalog has not been read, so it is not known that Start is ready.</summary>
    internal const string StartNoCatalog = "Start is not available until the command list is read.";

    /// <summary>The form needs a recipe before it can send.</summary>
    internal const string StartChooseRecipe = "Choose a workflow type";

    /// <summary>The form needs an item before it can send.</summary>
    internal const string StartChooseItem = "Choose the item to start it for";

    /// <summary>An input the form cannot send empty.</summary>
    /// <param name="label">The input's label.</param>
    /// <returns>The sentence.</returns>
    internal static string StartRequired(string label) => $"{label} is required";

    /// <summary>A payload input that is not JSON.</summary>
    /// <param name="label">The input's label.</param>
    /// <returns>The sentence.</returns>
    internal static string StartNotJson(string label) => $"{label} must be JSON";

    /// <summary>A link input that is not a link.</summary>
    /// <param name="label">The input's label.</param>
    /// <returns>The sentence.</returns>
    internal static string StartNotLink(string label) => $"{label} must be a link, such as https://example.com";

    /// <summary>A file or a text over the 1 MiB a fact may hold.</summary>
    /// <param name="label">The input's label.</param>
    /// <returns>The sentence.</returns>
    internal static string StartTooLarge(string label) => $"{label} is larger than 1 MiB";

    /// <summary>A file that could not be read.</summary>
    /// <param name="label">The input's label.</param>
    /// <returns>The sentence.</returns>
    internal static string StartUnreadable(string label) => $"{label}: the file could not be read";

    /// <summary>An attempt that is already live.</summary>
    /// <param name="address">Its address.</param>
    /// <returns>The sentence.</returns>
    internal static string StartAlreadyRunning(string address) => $"{address} is already running.";

    /// <summary>An attempt that already ended.</summary>
    /// <param name="address">Its address.</param>
    /// <returns>The sentence.</returns>
    internal static string StartFinished(string address) => $"{address} has finished. Start a new attempt to run it again.";

    /// <summary>A chosen file in one line.</summary>
    /// <param name="name">Its name.</param>
    /// <param name="size">Its size in bytes.</param>
    /// <returns>"prd.md · 1.2 KB".</returns>
    internal static string StartFileLine(string name, long size) =>
        name + " · " + (size < 1024 ? size.ToString(CultureInfo.InvariantCulture) + " B" : (size / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB");

    /// <summary>One workflow in the picker: its name alone (the id and version stay out of sight).</summary>
    /// <param name="name">Its display name.</param>
    /// <returns>The line.</returns>
    internal static string StartRecipeLine(string name) => name;

    /// <summary>One item in the picker.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="title">Its title.</param>
    /// <returns>The title, then the id in a muted tail.</returns>
    internal static string StartItemLine(string id, string title) => $"{title} · {id}";
}
