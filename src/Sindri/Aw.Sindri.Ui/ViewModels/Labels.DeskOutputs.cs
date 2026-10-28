namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the files a desk task's Complete carries as its required outputs. No em dashes, no internal terms.</summary>
internal static partial class Labels
{
    /// <summary>The Browse button's words.</summary>
    internal const string BrowseOutput = "Browse…";

    /// <summary>The file chooser's title for an output.</summary>
    internal const string ChooseOutputTitle = "Choose the file for the output";

    /// <summary>Said on a Home row when Complete needs a file first: the instance sheet has the box for it.</summary>
    internal const string CompleteNeedsOutput = "This task needs a file first: open it with Answer, pick the file there and complete it.";

    /// <summary>Said where Complete was pressed on a task that needs a file: the sheet opens, and its box takes the file.</summary>
    internal const string CompleteNeedsOutputInSheet = "This task needs a file first: pick it in the sheet and complete it there.";

    /// <summary>The words above one output's box.</summary>
    /// <param name="key">The output's key.</param>
    /// <returns>The words.</returns>
    internal static string OutputCaption(string key) => $"{key} (required): a path to the file";

    /// <summary>Said under the outputs while Complete waits for one.</summary>
    /// <param name="key">The first output not given yet.</param>
    /// <returns>The words.</returns>
    internal static string PickOutput(string key) => $"Pick {key}";

    /// <summary>The accessible name of an output's path box.</summary>
    /// <param name="key">The output's key.</param>
    /// <returns>The name.</returns>
    internal static string OutputBoxName(string key) => $"Output {key}";

    /// <summary>The accessible name of an output's Browse button.</summary>
    /// <param name="key">The output's key.</param>
    /// <returns>The name.</returns>
    internal static string OutputBrowseName(string key) => $"Browse for {key}";

    /// <summary>Said under a box whose file is larger than 1 MiB.</summary>
    /// <param name="key">The output's key.</param>
    /// <returns>The sentence.</returns>
    internal static string OutputTooLarge(string key) => $"{key} is larger than 1 MiB";

    /// <summary>Said under a box whose path can't be read.</summary>
    /// <param name="path">The path as typed.</param>
    /// <returns>The sentence.</returns>
    internal static string OutputUnreadable(string path) => $"Can't read {path}";
}
