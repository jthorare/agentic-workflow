using System.Collections.ObjectModel;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the New workflow form (#2550). A name and a starter template; Create sends once, then opens the new workflow in the editor sheet.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    /// <summary>True while the New workflow form is open.</summary>
    public bool IsCreating { get; private set => Set(ref field, value); }

    /// <summary>The starter templates the form offers.</summary>
    public ObservableCollection<TemplateChoice> Templates { get; } = [];

    /// <summary>The name typed for the new workflow (its slug).</summary>
    public string NewSlug { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The template picked, by name.</summary>
    public string SelectedTemplate { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>True when Create does something: the name is valid and no create is on the way.</summary>
    public bool CanCreate => throw new NotSupportedException();

    /// <summary>Why Create is off; empty when it is on.</summary>
    public string CreateReasonText => throw new NotSupportedException();

    /// <summary>The server's reason when the last create was refused; empty otherwise.</summary>
    public string CreateError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while a create is on the way.</summary>
    public bool IsCreateSending { get; private set => Set(ref field, value); }

    /// <summary>Opens the form.</summary>
    internal void OpenCreate() => throw new NotSupportedException();

    /// <summary>Closes the form and forgets what was typed.</summary>
    internal void CancelCreate() => throw new NotSupportedException();

    /// <summary>Sends the create once, reads the list, opens the new workflow and its editor sheet; a refusal shows the server's reason in the form.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Create(CancellationToken cancellationToken) => throw new NotSupportedException();
}
