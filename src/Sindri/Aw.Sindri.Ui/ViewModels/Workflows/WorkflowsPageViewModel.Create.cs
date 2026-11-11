using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The Workflows page (<see cref="WorkflowsPageViewModel"/>, continued): the New workflow form (#2550). A name, a short id filled from it (until the person edits it) and a starter template.
/// Create sends once, sets the name on the new workflow, then opens the workflow's page; a refusal shows Sindri's reason in the form.
/// </summary>
internal sealed partial class WorkflowsPageViewModel
{
    private string _newName = string.Empty;
    private string _newSlug = string.Empty;
    private const string SlugTaken = "SND-GRAPH-009";

    private HashSet<string> _held = new(StringComparer.Ordinal);   // every short id Sindri holds, archived ones too (an archived slug stays reserved)
    private string? _refusedId;   // the short id Sindri refused as taken: Create stays off until the short id changes
    private bool _slugEdited;   // once the person types in the short id, the name no longer overwrites it

    /// <summary>True while the New workflow form is open.</summary>
    public bool IsCreating { get; private set { Set(ref field, value); OnChanged(nameof(ShowsCreateForm)); OnChanged(nameof(ShowsEmpty)); OnChanged(nameof(ShowsHeaderNew)); } }

    /// <summary>The starter templates the form offers.</summary>
    public ObservableCollection<TemplateChoice> Templates { get; } = [];

    /// <summary>The name typed for the new workflow: any text. The short id follows it until the person edits that. Typing clears an earlier refusal.</summary>
    public string NewName
    {
        get => _newName;
        set
        {
            Set(ref _newName, value);
            if (!_slugEdited)   // a hand-edited short id survives the name being cleared and retyped: only emptying the short id itself gives it back to the name
            {
                _newSlug = WorkflowSlugs.From(value);
                OnChanged(nameof(NewSlug));
            }

            ClearCreateError();
            NotifyCreate();
        }
    }

    /// <summary>The short id (the slug): what commands and branches call the workflow. Editing it by hand stops the name from filling it.</summary>
    public string NewSlug
    {
        get => _newSlug;
        set
        {
            if (string.Equals(value, _newSlug, StringComparison.Ordinal))
            {
                return;   // the view echoing what the name filled in is not an edit
            }

            // Emptying the field gives it back to the name: the short id is derived from it again.
            _slugEdited = value.Length > 0;
            Set(ref _newSlug, _slugEdited ? value : WorkflowSlugs.From(_newName));
            ClearCreateError();
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

    /// <summary>True when Create does something: a name and a valid short id, and no create on the way.</summary>
    public bool CanCreate => CreateReasonText.Length == 0 && !IsCreateSending && SelectedTemplate.Length > 0 && !IsRefusedId;

    private bool IsRefusedId => string.Equals(NewSlug, _refusedId, StringComparison.Ordinal);

    /// <summary>Why Create is off because of the name or short id; empty when both are fine.</summary>
    public string CreateReasonText => ReasonFor();

    /// <summary>True when the reason line shows: there is a reason and no refusal line already says it (a refused id has both, in the same words), so no empty or doubled line sits above the buttons.</summary>
    public bool ShowsCreateReason => CreateReasonText.Length > 0 && !HasCreateError;

    /// <summary>True when the reason is a problem with what was typed (an error colour), false for the hint before a name is typed.</summary>
    public bool CreateReasonIsError => CreateReasonText.Length > 0 && NewName.Trim().Length > 0;

    /// <summary>True when the short id field is marked invalid: a reason about it, or a refusal.</summary>
    public bool ShortIdInvalid => CreateReasonIsError || HasCreateError;

    /// <summary>Sindri's reason when the last create was refused, without its code; empty otherwise.</summary>
    public string CreateError { get; private set { Set(ref field, value); OnChanged(nameof(HasCreateError)); OnChanged(nameof(ShowsCreateReason)); OnChanged(nameof(ShortIdInvalid)); } } = string.Empty;

    /// <summary>The refusal's code (SND-…): the line's tooltip, never part of the sentence.</summary>
    public string CreateErrorCode { get; private set => Set(ref field, value); } = string.Empty;

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

    /// <summary>Opens the form with the first template picked. It does nothing while the views are stalled: the list can't say which short ids are taken.</summary>
    internal void OpenCreate()
    {
        if (ViewsStalled)
        {
            return;
        }

        if (Templates.Count == 0)
        {
            foreach (var template in Labels.WfTemplates)
            {
                Templates.Add(template);
            }
        }

        SelectedTemplate = Templates[0].Name;
        Forget();
        IsCreating = true;
        HeldRead = ReadHeld();
    }

    /// <summary>The read of every short id Sindri holds (the page's list shows live workflows only), started when the form opens.</summary>
    internal Task HeldRead { get; private set; } = Task.CompletedTask;

    // The taken-id check also needs the archived ids: asked with includeArchived, for this check only. A read that fails leaves the check on the live list, and the server's refusal is the fallback.
    private async Task ReadHeld()
    {
        if (_url is not { } url)
        {
            return;
        }

        var all = await Api.WorkflowsIncludingArchived(url, CancellationToken.None).ConfigureAwait(true);
        if (all.IsSuccess)
        {
            _held = [.. all.Value.Workflows.Select(static w => w.Slug)];
            NotifyCreate();
        }
    }

    /// <summary>Closes the form and forgets what was typed.</summary>
    internal void CancelCreate()
    {
        IsCreating = false;
        Forget();
    }

    /// <summary>
    /// Sends the create once (a human action, never retried; a second press while it is on the way does nothing), sets the name the person typed, reads the list and opens the new workflow's page (its graph, Run now, Edit), the editor sheet closed.
    /// A refusal shows Sindri's reason in the form and keeps what was typed. A name that could not be set is said on the page; the workflow stays.
    /// </summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Create(CancellationToken cancellationToken)
    {
        if (_url is not { } url || !CanCreate)
        {
            return;
        }

        var (name, slug) = (NewName.Trim(), NewSlug);
        IsCreateSending = true;
        ClearCreateError();
        try
        {
            var made = await Api.CreateWorkflow(url, slug, SelectedTemplate, cancellationToken).ConfigureAwait(true);
            if (made.IsError)
            {
                await CreateRefused(url, made.Error, slug, cancellationToken).ConfigureAwait(true);
                return;
            }

            var renamed = await Api.ApplyWorkflowOps(url, made.Value.Slug, made.Value.Revision, SetName(name), cancellationToken).ConfigureAwait(true);
            var note = renamed.IsError ? Labels.WfCreateRenameFailed(renamed.Error) : string.Empty;
            IsCreating = false;
            Forget();
            await ReadList(url, cancellationToken).ConfigureAwait(true);
            await OpenWorkflow(made.Value.Slug, cancellationToken).ConfigureAwait(true);
            if (note.Length > 0)
            {
                Runs.Note(note);
            }
        }
        finally
        {
            IsCreateSending = false;
        }
    }

    // One op, set_meta: the template's own name is replaced by the one typed. Sent on the revision the create made.
    private static string SetName(string name) => new JsonArray((JsonNode)new JsonObject { ["op"] = "set_meta", ["name"] = name }).ToJsonString();

    // A refusal says why. When Sindri didn't answer, the workflow may exist, so the list is read again and the line says so: a second press could be refused as a taken id.
    private async Task CreateRefused(Uri url, Error error, string slug, CancellationToken cancellationToken)
    {
        CreateErrorCode = error.Code;
        if (string.Equals(error.Code, SindriUiErrors.Unreachable.Code, StringComparison.Ordinal))
        {
            CreateError = Labels.WfCreateUnsure(error);
            await ReadList(url, cancellationToken).ConfigureAwait(true);
            return;
        }

        if (string.Equals(error.Code, SlugTaken, StringComparison.Ordinal))
        {
            _refusedId = slug;   // the same short id would be refused again: Create waits for it to change
            CreateError = Labels.WfShortIdTaken(slug);
            NotifyCreate();
            return;
        }

        CreateError = Labels.WfCreateRefused(error);
    }

    private void ClearCreateError() => (CreateError, CreateErrorCode) = (string.Empty, string.Empty);

    private void Forget()
    {
        (_newName, _newSlug, _slugEdited, _refusedId) = (string.Empty, string.Empty, false, null);
        ClearCreateError();
        OnChanged(nameof(NewName));
        OnChanged(nameof(NewSlug));
        NotifyCreate();
    }

    private void NotifyCreate()
    {
        foreach (var name in new[] { nameof(CreateReasonText), nameof(ShowsCreateReason), nameof(CreateReasonIsError), nameof(ShortIdInvalid), nameof(CanCreate) })
        {
            OnChanged(name);
        }
    }

    // A short id the page lists, one Sindri holds (archived workflows keep theirs reserved), or one Sindri just refused as taken: the reason stays on screen as long as Create stays off for it.
    private bool IsTaken(string id) => IsRefusedId || _held.Contains(id) || Rows.Any(r => string.Equals(r.Slug, id, StringComparison.Ordinal));

    // What stops Create, in words: a hint until a name is typed, then the short id's problems (the rule Sindri holds: WorkflowSlug; "docs" and "blocks" are routes of their own).
    private string ReasonFor()
    {
        if (NewName.Trim().Length == 0)
        {
            return Labels.WfNameEmpty;
        }

        var id = NewSlug;
        if (id.Length == 0)
        {
            return Labels.WfShortIdEmpty;
        }

        if (string.Equals(id, "docs", StringComparison.Ordinal) || string.Equals(id, "blocks", StringComparison.Ordinal))
        {
            return Labels.WfShortIdReserved(id);
        }

        if (!(id.Length <= WorkflowSlugs.Max && WorkflowSlugs.IsSlugStart(id[0]) && id.All(static c => WorkflowSlugs.IsSlugStart(c) || c == '-')))
        {
            return Labels.WfShortIdShape;
        }

        return IsTaken(id) ? Labels.WfShortIdTaken(id) : string.Empty;
    }
}
