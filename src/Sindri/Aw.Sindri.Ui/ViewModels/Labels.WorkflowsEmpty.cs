using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the Workflows page's empty state (<see cref="Labels"/>, continued).</summary>
internal static partial class Labels
{
    /// <summary>The empty state's create action.</summary>
    internal const string WfNew = "New workflow";

    /// <summary>The name field's label.</summary>
    internal const string WfNewName = "Name";

    /// <summary>The short id field's label: the name commands and branches use, filled from the name.</summary>
    internal const string WfNewShortId = "Short id";

    /// <summary>The template picker's label.</summary>
    internal const string WfNewTemplate = "Start from";

    /// <summary>The form's Create action.</summary>
    internal const string WfCreate = "Create";

    /// <summary>The form's Cancel action.</summary>
    internal const string WfCreateCancel = "Cancel";

    /// <summary>Why Create is off before a name is typed (a hint, not an error).</summary>
    internal const string WfNameEmpty = "Type a name to create the workflow.";

    /// <summary>Why Create is off when the name gives no usable id.</summary>
    internal const string WfShortIdEmpty = "Type a short id: lowercase letters, digits and '-'.";

    /// <summary>Why Create is off for a short id Sindri would refuse.</summary>
    internal const string WfShortIdShape = "Use lowercase letters, digits and '-' in the short id, starting with a letter or digit, up to 63 characters.";

    /// <summary>Why Create is off for a short id that is a route of its own.</summary>
    /// <param name="id">The short id.</param>
    /// <returns>The words.</returns>
    internal static string WfShortIdReserved(string id) => $"'{id}' is kept for the workflow docs; pick another short id.";

    /// <summary>Why Create is off for a short id another workflow already has.</summary>
    /// <param name="id">The short id.</param>
    /// <returns>The words.</returns>
    internal static string WfShortIdTaken(string id) => $"A workflow already uses '{id}'; pick another short id.";

    /// <summary>The form's line when Sindri refused the create; the code is the line's tooltip, not part of it.</summary>
    /// <param name="error">The refusal.</param>
    /// <returns>The words.</returns>
    internal static string WfCreateRefused(Error error) => "Couldn't create the workflow. " + Capitalised(error.Message);

    /// <summary>The form's line when Sindri didn't answer the create: the workflow may exist.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfCreateUnsure(Error error) => "Sindri didn't answer, so the workflow may exist; check the list before you press Create again. " + Capitalised(error.Message);

    /// <summary>The page's line when the workflow was made but its name could not be set.</summary>
    /// <param name="error">The refusal of the rename.</param>
    /// <returns>The words.</returns>
    internal static string WfCreateRenameFailed(Error error) => "Created, but the name couldn't be set: " + error.Message;

    /// <summary>The list level's line while the views are stalled (the Rebuild action is beside it).</summary>
    internal const string WfStalled = "Workflows can't load while the lists are stalled.";

    /// <summary>The starter templates Sindri accepts (there is no list route yet; GraphTemplates names them), with their titles and what each does.</summary>
    internal static IReadOnlyList<TemplateChoice> WfTemplates { get; } =
    [
        new("weekday-check", "Weekday check", "Every weekday at 7:00 ask the model a question and send a notification only when its answer is flagged."),
        new("daily-digest", "Daily digest", "Every day at 9:00 ask the model for a summary and send it as a notification."),
        new("manual-notify", "Manual notify", "A button you press: it sends one notification. The smallest workflow to start from."),
    ];

    /// <summary>The line before the agent command.</summary>
    internal const string WfAgentRoute = "Agents can make one with";

    /// <summary>The command an agent runs to make a workflow.</summary>
    internal const string WfAgentCommand = "sindri wf create";

    /// <summary>An actor as the person reads it: "you" for the person's own ("human:ui"), else as served.</summary>
    /// <param name="by">The served actor.</param>
    /// <returns>The words.</returns>
    internal static string WfActor(string by) => by.StartsWith("human:", StringComparison.Ordinal) ? "you" : by;
}
