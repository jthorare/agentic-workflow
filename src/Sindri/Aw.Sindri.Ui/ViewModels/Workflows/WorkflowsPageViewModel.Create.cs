using System.Collections.ObjectModel;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the New workflow form (#2550). A name and a starter template; Create sends once, then opens the new workflow in the editor sheet.</summary>
internal sealed partial class WorkflowsPageViewModel
{
    private const int SlugMax = 63;

    private string _newSlug = string.Empty;

    /// <summary>True while the New workflow form is open.</summary>
    public bool IsCreating { get; private set => Set(ref field, value); }

    /// <summary>The starter templates the form offers.</summary>
    public ObservableCollection<TemplateChoice> Templates { get; } = [];

    /// <summary>The name typed for the new workflow (its slug). Typing clears an earlier refusal.</summary>
    public string NewSlug
    {
        get => _newSlug;
        set
        {
            Set(ref _newSlug, value);
            CreateError = string.Empty;
            NotifyCreate();
        }
    }

    /// <summary>The template picked, by name.</summary>
    public string SelectedTemplate
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(SelectedChoice));
            OnChanged(nameof(SelectedDescription));
        }
    } = string.Empty;

    /// <summary>The template picked, for the picker.</summary>
    public TemplateChoice? SelectedChoice
    {
        get => Templates.FirstOrDefault(t => string.Equals(t.Name, SelectedTemplate, StringComparison.Ordinal));
        set
        {
            if (value is not null)
            {
                SelectedTemplate = value.Name;
            }
        }
    }

    /// <summary>What the picked template does, in one line.</summary>
    public string SelectedDescription => SelectedChoice?.Description ?? string.Empty;

    /// <summary>True when Create does something: the name is valid and no create is on the way.</summary>
    public bool CanCreate => CreateReasonText.Length == 0 && !IsCreateSending && SelectedTemplate.Length > 0;

    /// <summary>Why Create is off because of the name; empty when the name is valid.</summary>
    public string CreateReasonText => NameProblem(NewSlug);

    /// <summary>The server's reason when the last create was refused; empty otherwise.</summary>
    public string CreateError { get; private set { Set(ref field, value); OnChanged(nameof(HasCreateError)); } } = string.Empty;

    /// <summary>True when the last create was refused.</summary>
    public bool HasCreateError => CreateError.Length > 0;

    /// <summary>True while a create is on the way.</summary>
    public bool IsCreateSending
    {
        get;
        private set
        {
            Set(ref field, value);
            NotifyCreate();
        }
    }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The name field's label.</summary>
    public string NewSlugLabel => Labels.WfNewName;

    /// <summary>The template picker's label.</summary>
    public string TemplateLabel => Labels.WfNewTemplate;

    /// <summary>The Create button's words.</summary>
    public string CreateText => Labels.WfCreate;

    /// <summary>The Cancel button's words.</summary>
    public string CancelCreateText => Labels.WfCreateCancel;
#pragma warning restore CA1822, MA0041

    /// <summary>Opens the form with the first template picked.</summary>
    internal void OpenCreate()
    {
        if (Templates.Count == 0)
        {
            foreach (var template in Labels.WfTemplates)
            {
                Templates.Add(template);
            }
        }

        SelectedTemplate = Templates[0].Name;
        (_newSlug, CreateError) = (string.Empty, string.Empty);
        OnChanged(nameof(NewSlug));
        NotifyCreate();
        IsCreating = true;
    }

    /// <summary>Closes the form and forgets what was typed.</summary>
    internal void CancelCreate()
    {
        IsCreating = false;
        _newSlug = string.Empty;
        CreateError = string.Empty;
        OnChanged(nameof(NewSlug));
        NotifyCreate();
    }

    /// <summary>
    /// Sends the create once (a human action, never retried; a second press while it is on the way does nothing), reads the list, opens the new workflow and its editor sheet.
    /// A refusal shows Sindri's reason in the form and keeps the name.
    /// </summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Create(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanCreate)
        {
            return;
        }

        IsCreateSending = true;
        CreateError = string.Empty;
        try
        {
            var made = await Api.CreateWorkflow(url, NewSlug, SelectedTemplate, cancellationToken).ConfigureAwait(true);
            if (made.IsError)
            {
                await CreateRefused(url, made.Error, cancellationToken).ConfigureAwait(true);
                return;
            }

            IsCreating = false;
            _newSlug = string.Empty;
            OnChanged(nameof(NewSlug));
            await ReadList(url, cancellationToken).ConfigureAwait(true);
            await OpenWorkflow(made.Value.Slug, cancellationToken).ConfigureAwait(true);
            await Edit(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsCreateSending = false;
        }
    }

    // A refusal says why. When Sindri didn't answer, the workflow may exist, so the list is read again and the line says so: a second press could be refused as a taken name.
    private async Task CreateRefused(Uri url, Error error, CancellationToken cancellationToken)
    {
        if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            CreateError = Labels.WfCreateUnsure(error);
            await ReadList(url, cancellationToken).ConfigureAwait(true);
            return;
        }

        CreateError = Labels.WfCreateRefused(error);
    }

    private void NotifyCreate()
    {
        OnChanged(nameof(CreateReasonText));
        OnChanged(nameof(CanCreate));
    }

    // The slug rule Sindri holds (WorkflowSlug): lowercase letters, digits and '-', a letter or digit first, at most 63; "docs" and "blocks" are routes of their own.
    private static string NameProblem(string name)
    {
        if (name.Length == 0)
        {
            return Labels.WfNameEmpty;
        }

        if (string.Equals(name, "docs", StringComparison.Ordinal) || string.Equals(name, "blocks", StringComparison.Ordinal))
        {
            return Labels.WfNameReserved(name);
        }

        return name.Length <= SlugMax && IsSlugStart(name[0]) && name.All(static c => IsSlugStart(c) || c == '-') ? string.Empty : Labels.WfNameShape;
    }

    private static bool IsSlugStart(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
