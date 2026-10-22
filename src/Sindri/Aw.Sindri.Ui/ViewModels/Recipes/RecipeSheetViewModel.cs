using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>The recipe sheet (screens spec §4.3, U10a): one recipe read by id, and by a hash when the link names one. Read-only.</summary>
internal sealed partial class RecipeSheetViewModel(ISindriApi api, IClipboard? clipboard = null) : ObservableObject
{
    private Uri? _url;
    private string _subject = string.Empty;
    private int _generation;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(IsProblem));
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(IsShown));
        }
    }

    /// <summary>True while a refusal or an unreadable answer is shown.</summary>
    public bool IsProblem => Phase is FramePhase.NotFound or FramePhase.Unreadable or FramePhase.Refused;

    /// <summary>True when the recipe serves a name template.</summary>
    public bool HasName => Name.Length > 0;

    /// <summary>True before the first answer.</summary>
    public bool IsLoading => Phase == FramePhase.Loading;

    /// <summary>True while a recipe is shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>The name template's line, "name {fact:scope} - {start:date}"; empty when the recipe serves none.</summary>
    public string NameLine => HasName ? Labels.NameTemplateLine(Name) : string.Empty;

    /// <summary>True when there is a note.</summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>Back's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string BackText => Labels.Back;

    /// <summary>The heading of the loading skeleton.</summary>
    public string LoadingText => Labels.LoadingRecipe;
#pragma warning restore CA1822, MA0041

    /// <summary>The recipe's id.</summary>
    public string Id { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The workflow's name as a person reads it: the served display name, else the id made readable; empty before a read.</summary>
    public string Title { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the Details (the id, hash, version, urgency, name template and each input's key and type) show; closed until opened, and kept across re-reads.</summary>
    public bool IsDetailsOpen { get; set => Set(ref field, value); }

    /// <summary>The Details disclosure's header.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string DetailsText => Labels.RecipeDetails;
#pragma warning restore CA1822, MA0041

    /// <summary>Opens the Details, or closes them.</summary>
    internal void ToggleDetails() => IsDetailsOpen = !IsDetailsOpen;

    /// <summary>The header's identity line (in Details).</summary>
    public string Identity { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The name template, or empty.</summary>
    public string Name { get; private set { Set(ref field, value); OnChanged(nameof(HasName)); OnChanged(nameof(NameLine)); } } = string.Empty;

    /// <summary>The states band; null before a read.</summary>
    public RecipeStatesViewModel? States { get; private set => Set(ref field, value); }

    /// <summary>The inputs and outputs; null before a read.</summary>
    public RecipeFactsViewModel? Facts { get; private set => Set(ref field, value); }

    /// <summary>The sentence a refusal or an unreadable answer shows.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The note under the header; empty when none.</summary>
    public string Note { get; private set { Set(ref field, value); OnChanged(nameof(HasNote)); } } = string.Empty;

    /// <summary>Opens a recipe and reads it.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="subject">The link's subject: an id, or an id, '@' and a hash.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Open(Uri url, string subject, CancellationToken cancellationToken)
    {
        Show(subject);
        _url = url;
        return Read(cancellationToken);
    }

    /// <summary>Names the recipe now, before any read (a deep link, even before the app is connected).</summary>
    /// <param name="subject">The link's subject: an id, or an id, '@' and a hash.</param>
    internal void Show(string subject)
    {
        if (!string.Equals(subject, _subject, StringComparison.Ordinal))
        {
            Clear();
            _subject = subject;
            Id = Split(subject).Recipe;
            Title = RecipeNames.Of(Id, displayName: null);
            Description = RecipeNames.Does(Id);
            Phase = FramePhase.Loading;
        }
    }

    /// <summary>Re-reads the recipe shown.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the sheet shows the answer.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        _url = url;
        return _subject.Length == 0 ? Task.CompletedTask : Read(cancellationToken);
    }

    private static (string Recipe, string? Hash) Split(string subject)
    {
        var at = subject.IndexOf('@', StringComparison.Ordinal);
        return at < 0 ? (subject, null) : (subject[..at], subject[(at + 1)..]);
    }

    private async Task Read(CancellationToken cancellationToken)
    {
        if (_url is not { } url)
        {
            return;
        }

        var (recipe, hash) = Split(_subject);
        var generation = ++_generation;
        var answer = await api.Recipe(url, recipe, hash, cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;   // a newer read, or another recipe, took over
        }

        if (answer.IsSuccess)
        {
            Show(answer.Value);
            await ReadStanding(url, recipe, hash, generation, cancellationToken).ConfigureAwait(true);
            await ReadStats(url, recipe, generation, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            Failed(answer.Error);
        }
    }

    private void Show(RecipeView recipe)
    {
        Id = recipe.Id;
        Title = RecipeNames.Of(recipe.Id, recipe.DisplayName);
        Description = RecipeNames.Does(recipe.Id);
        Identity = string.Create(CultureInfo.InvariantCulture, $"{recipe.Id}@{Labels.Hash12(recipe.Hash)} · v{recipe.Version} · {Labels.UrgencyWord(recipe.Urgency)}");
        Name = recipe.Name ?? string.Empty;
        _shownHash = Labels.Hash12(recipe.Hash);   // the figures are the shown version's: a prefix of its digits
        States = new RecipeStatesViewModel(recipe);
        Facts = new RecipeFactsViewModel(recipe);
        Note = string.Empty;
        ProblemText = string.Empty;
        Phase = FramePhase.Shown;
    }

    private void Failed(Error error)
    {
        if (Phase == FramePhase.Shown)
        {
            Note = error.Kind == ErrorKind.NotFound ? Labels.RecipeGone : Note;   // what was shown stays
        }
        else if (error.Kind == ErrorKind.NotFound)
        {
            Problem(FramePhase.NotFound, error.Message);
        }
        else if (string.Equals(error.Code, SindriUiErrors.BadAnswer.Code, StringComparison.Ordinal))
        {
            Problem(FramePhase.Unreadable, Labels.RecipeUnreadable);
        }
        else
        {
            Problem(FramePhase.Refused, Labels.InstanceRefused(error));
        }
    }

    private void Problem(FramePhase phase, string text)
    {
        ProblemText = text;
        Phase = phase;
    }

    private void Clear()
    {
        _generation++;
        (Id, Title, Identity, Name, Note, ProblemText, Description) = (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        (States, Facts) = (null, null);
        ClearStanding();
        ClearStats();
    }
}
