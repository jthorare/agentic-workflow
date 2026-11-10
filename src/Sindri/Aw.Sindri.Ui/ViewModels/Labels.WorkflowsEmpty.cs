using Aw.Kernel;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The words of the Workflows page's empty state (<see cref="Labels"/>, continued).</summary>
internal static partial class Labels
{
    /// <summary>The empty state's create action.</summary>
    internal const string WfNew = "New workflow";

    /// <summary>The name field's label.</summary>
    internal const string WfNewName = "Workflow name";

    /// <summary>The template picker's label.</summary>
    internal const string WfNewTemplate = "Start from";

    /// <summary>The form's Create action.</summary>
    internal const string WfCreate = "Create";

    /// <summary>The form's Cancel action.</summary>
    internal const string WfCreateCancel = "Cancel";

    /// <summary>Why Create is off before a name is typed.</summary>
    internal const string WfNameEmpty = "Type a name to create the workflow.";

    /// <summary>Why Create is off for a name Sindri would refuse.</summary>
    internal const string WfNameShape = "Use lowercase letters, digits and '-', start with a letter or digit, up to 63 characters.";

    /// <summary>Why Create is off for a name that is a route of its own.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The words.</returns>
    internal static string WfNameReserved(string name) => $"'{name}' is kept for the workflow docs; pick another name.";

    /// <summary>The form's line when Sindri refused the create.</summary>
    /// <param name="error">The refusal.</param>
    /// <returns>The words.</returns>
    internal static string WfCreateRefused(Error error) => "Couldn't create the workflow. " + InstanceRefused(error);

    /// <summary>The form's line when Sindri didn't answer the create: the workflow may exist.</summary>
    /// <param name="error">The failure.</param>
    /// <returns>The words.</returns>
    internal static string WfCreateUnsure(Error error) => "Sindri didn't answer, so the workflow may exist; check the list before you press Create again. " + InstanceRefused(error);

    /// <summary>The starter templates Sindri accepts (there is no list route yet), with what each does.</summary>
    internal static IReadOnlyList<TemplateChoice> WfTemplates { get; } =
    [
        new("weekday-check", "Every weekday at 7:00 ask the model a question and send a notification only when its answer is flagged."),
        new("daily-digest", "Every day at 9:00 ask the model for a summary and send it as a notification."),
        new("manual-notify", "A button you press: it sends one notification. The smallest workflow to start from."),
    ];

    /// <summary>The line before the agent command.</summary>
    internal const string WfAgentRoute = "Agents can make one with";

    /// <summary>The command an agent runs to make a workflow.</summary>
    internal const string WfAgentCommand = "sindri wf create";
}
