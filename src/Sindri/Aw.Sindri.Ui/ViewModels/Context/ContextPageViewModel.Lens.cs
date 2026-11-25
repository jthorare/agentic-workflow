using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The lens stack (U9a): the anchor box's read of GET /sindri/context/lenses and what it draws, read-only.</summary>
internal sealed partial class ContextPageViewModel
{
    private int _generation;

    /// <summary>The map of the last answer (F3 spec 1), or null before a read and after a refusal.</summary>
    internal ContextMapViewModel? Map { get; private set; }

    /// <summary>The anchors the map does not hold, each as "not found".</summary>
    public IReadOnlyList<ContextLine> Missing { get; private set; } = [];

    /// <summary>The served suggestions after a refusal, as choices (the anchors to pick).</summary>
    public IReadOnlyList<string> Choices { get; private set; } = [];

    /// <summary>The <see cref="Choices"/> with their titles and kinds, as the "Did you mean" row draws them (#2809).</summary>
    public IReadOnlyList<ContextChoice> ChoiceRows =>
        [.. Choices.Select(k => _choiceTitles.TryGetValue(k, out var known) ? known : new ContextChoice(k, k, string.Empty))];

    // The titles and kinds the server sent with its suggestions (a refusal's 'choices', a question's 'suggestions'), by key (#2809).
    private readonly Dictionary<string, ContextChoice> _choiceTitles = new(StringComparer.Ordinal);

    /// <summary>The refusal's own words, or empty.</summary>
    public string Problem { get; private set; } = string.Empty;

    /// <summary>Whether a map is drawn.</summary>
    public bool HasStack => Map is not null;

    /// <summary>What the answer searched and why the rest was not; empty on an exact hit or when every area was searched.</summary>
    public string Coverage => Map?.Coverage ?? string.Empty;

    /// <summary>Whether there is a coverage sentence to show.</summary>
    public bool HasCoverage => Coverage.Length > 0;

    /// <summary>Whether the map drawn is the draft one (the page head says so).</summary>
    public bool IsDraft => Map?.Draft == true;

    /// <summary>The page head's draft mark.</summary>
    public string DraftTag { get; } = Labels.ContextMapDraftTag;

    /// <summary>Whether the refusal offers choices.</summary>
    public bool HasChoices => Choices.Count > 0;

    /// <summary>Whether there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>True when the last search found nothing, or several items: a plain sentence with one action, not the refusal banner (#3677).</summary>
    public bool IsMiss => HasProblem
        && (string.Equals(_problemCode, "SND-CONTEXT-003", StringComparison.Ordinal) || string.Equals(_problemCode, "SND-CONTEXT-037", StringComparison.Ordinal));

    /// <summary>Whether the refusal banner shows: a refusal that is not a miss.</summary>
    public bool ShowBanner => HasProblem && !IsMiss;

    /// <summary>The choices' name.</summary>
    public string ChoicesName { get; } = Labels.ContextSuggestions;

    /// <summary>Reads the lenses for what is in the box (Enter): a question is asked first for its anchors, anchor-shaped text is read as it is. One read, the newest answer wins.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public async Task Submit(CancellationToken cancellationToken = default)
    {
        var words = Words(Asked);
        if (words.Count == 0)
        {
            _ = BackToWholeMap();   // Enter on an empty box returns to the full map
            return;
        }

        if (Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var generation = ++_generation;   // a read still in flight must not draw over this one
        if (ShowConcepts)
        {
            await SearchConcepts(api, words, generation, cancellationToken).ConfigureAwait(true);   // the concept layer has no nodes to draw: its own filter
            return;
        }

        if (await Resolve(api, words, generation, cancellationToken).ConfigureAwait(true) is not var (anchors, fromQuestion, suggested))
        {
            return;
        }

        var result = await Ports.Api.Lenses(api, new LensesQuery(anchors, Depth: null, Draft: false, MapVersion: null), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        await Draw(api, result, fromQuestion, suggested, cancellationToken).ConfigureAwait(true);
    }

    // The answer onto the page: the map, or the refusal with its suggestions (the read's own, else the question's).
    private async Task Draw(Uri api, Result<LensesView> result, bool fromQuestion, List<string> suggested, CancellationToken cancellationToken)
    {
        if (result.IsSuccess)
        {
            Show(result.Value, choices: [], fromQuestion);
            ShowAnswerFiles(fromQuestion);
            NoteRead();
            await ZoomIntoWhole(api, cancellationToken).ConfigureAwait(true);
        }
        else if (string.Equals(result.Error.Code, NoMapCode, StringComparison.Ordinal))
        {
            Show(view: null, choices: []);
            ShowAnswerFiles(fromQuestion);
            ShowProblem(result.Error);
            await NoteRefusal(api, result.Error, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            // A miss is said in plain words and the map that was on the page stays: nothing is cleared or dimmed for it.
            ShowAnswerFiles(fromQuestion);
            Choices = Suggestions(result.Error) is { Count: > 0 } own ? own : suggested;
            ShowProblem(result.Error, Labels.ContextRefusal(result.Error.Code, result.Error.Message, AnchorText.Trim(), hasChoices: Choices.Count > 0));
            await NoteRefusal(api, result.Error, cancellationToken).ConfigureAwait(true);
        }

        Refresh();
    }

    /// <summary>Picks a suggested anchor: it replaces the box and is read.</summary>
    /// <param name="anchor">The suggested anchor.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public Task Pick(string anchor, CancellationToken cancellationToken = default) => Pick(anchor, title: null, cancellationToken);

    /// <summary>Reads a suggested anchor, then shows its title in the box, not its key.</summary>
    /// <param name="anchor">The anchor's key.</param>
    /// <param name="title">What the box should say once it is read; null leaves the key.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public Task Pick(string anchor, string? title, CancellationToken cancellationToken = default)
    {
        // The box shows the title; what is read is the key (a person typing the same title later reads it as a question).
        _picked = title is null ? null : (anchor, title);
        _picking = true;
        AnchorText = title ?? anchor;
        _picking = false;
        return Submit(cancellationToken);
    }

    private (string Key, string Title)? _picked;

    private bool _picking;

    // What the box asks: the key of a chip whose title it still shows, else what is typed.
    private string Asked => _picked is { } p && string.Equals(AnchorText, p.Title, StringComparison.Ordinal) ? p.Key : AnchorText;

    private List<string> Suggestions(Error error)
    {
        var name = string.Equals(error.Code, "SND-CONTEXT-003", StringComparison.Ordinal) ? "suggestedAnchors"
            : string.Equals(error.Code, "SND-CONTEXT-037", StringComparison.Ordinal) ? "candidates"
            : null;
        if (name is null || error.Extensions is null)
        {
            return [];
        }

        foreach (var choice in error.Extensions.Where(m => string.Equals(m.Key, "choices", StringComparison.Ordinal) && m.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(m => m.Value.EnumerateArray()).Select(Choice).OfType<ContextChoice>())
        {
            _choiceTitles[choice.Key] = choice;
        }

        return [.. error.Extensions.Where(m => string.Equals(m.Key, name, StringComparison.Ordinal) && m.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(m => m.Value.EnumerateArray()).Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString() ?? string.Empty)];
    }

    // One served choice; null when it has no key. A missing or blank label shows the key.
    private static ContextChoice? Choice(JsonElement e)
    {
        var key = Text(e, "key");
        if (key.Length == 0)
        {
            return null;
        }

        var label = Text(e, "label");
        return new ContextChoice(key, label.Length == 0 ? key : label, Text(e, "kind"));
    }

    private static string Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? string.Empty).Trim() : string.Empty;

    /// <summary>Keeps the titles a question's answer sent with its suggestions.</summary>
    private void KeepTitles(IReadOnlyList<QueryChoiceView>? suggestions)
    {
        foreach (var s in suggestions ?? [])
        {
            if (!string.IsNullOrWhiteSpace(s.Key))
            {
                _choiceTitles[s.Key] = new ContextChoice(s.Key, string.IsNullOrWhiteSpace(s.Label) ? s.Key : s.Label.Trim(), s.Kind?.Trim() ?? string.Empty);
            }
        }
    }

    private void Show(LensesView? view, List<string> choices, bool fromQuestion = false)
    {
        ForgetSelection();
        SetProblem(string.Empty);
        Choices = choices;
        AnswerFiles = [];
        _answerFilesHidden = 0;
        StageMessage = string.Empty;
        if (view is null)
        {
            Head = Whole.IsReady ? Whole.Head : string.Empty;   // no neighbourhood: the whole map is what the page shows
            Map = null;
            Missing = [];
            return;
        }

        Head = fromQuestion ? Labels.ContextFromQuestion(view.Anchors.Count) : Labels.ContextHead(view.Anchors.Count);
        Map = new ContextMapViewModel(view, exact: !fromQuestion) { Chosen = key => _ = Select(key) };
        Missing = [.. (view.MissingAnchors ?? []).Select(a => new ContextLine($"{a} · {Labels.ContextNotFound}"))];
    }

    private void Refresh()
    {
        OnChanged(nameof(Map));
        OnChanged(nameof(Head));
        OnChanged(nameof(AnswerFiles));
        OnChanged(nameof(HasAnswerFiles));
        OnChanged(nameof(AnswerFilesMore));
        OnChanged(nameof(HasAnswerFilesMore));
        OnChanged(nameof(StageMessage));
        OnChanged(nameof(HasStageMessage));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(HeadDetail));
        OnChanged(nameof(Missing));
        OnChanged(nameof(Choices));
        OnChanged(nameof(ChoiceRows));
        OnChanged(nameof(Problem));
        OnChanged(nameof(HasStack));
        OnChanged(nameof(Coverage));
        OnChanged(nameof(HasCoverage));
        OnChanged(nameof(MapVisible));
        OnChanged(nameof(ShowWhole));
        OnChanged(nameof(ShowWholeStatus));
        OnChanged(nameof(InspectorOpen));
        OnChanged(nameof(IsDraft));
        OnChanged(nameof(HasChoices));
        OnChanged(nameof(HasProblem));
        OnChanged(nameof(IsMiss));
        OnChanged(nameof(ShowBanner));
    }
}
