using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>The lens stack (U9a): the anchor box's read of GET /sindri/context/lenses and what it draws, read-only.</summary>
internal sealed partial class ContextPageViewModel
{
    private const int MaxAnchors = 20;
    private int _generation;

    /// <summary>The map of the last answer (F3 spec 1), or null before a read and after a refusal.</summary>
    internal ContextMapViewModel? Map { get; private set; }

    /// <summary>The anchors the map does not hold, each as "not found".</summary>
    public IReadOnlyList<ContextLine> Missing { get; private set; } = [];

    /// <summary>The served suggestions after a refusal, as choices (the anchors to pick).</summary>
    public IReadOnlyList<string> Choices { get; private set; } = [];

    /// <summary>The refusal's own words, or empty.</summary>
    public string Problem { get; private set; } = string.Empty;

    /// <summary>Whether a map is drawn.</summary>
    public bool HasStack => Map is not null;

    /// <summary>Whether the map drawn is the draft one (the page head says so).</summary>
    public bool IsDraft => Map?.Draft == true;

    /// <summary>The page head's draft mark.</summary>
    public string DraftTag { get; } = Labels.ContextMapDraftTag;

    /// <summary>Whether the refusal offers choices.</summary>
    public bool HasChoices => Choices.Count > 0;

    /// <summary>Whether there is a refusal to show.</summary>
    public bool HasProblem => Problem.Length > 0;

    /// <summary>The choices' name.</summary>
    public string ChoicesName { get; } = Labels.ContextSuggestions;

    /// <summary>Reads the lenses for what is in the box (Enter): a question is asked first for its anchors, anchor-shaped text is read as it is. One read, the newest answer wins.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public async Task Submit(CancellationToken cancellationToken = default)
    {
        var words = Words(AnchorText);
        if (words.Count == 0 || Ports.ApiUrl() is not { } api)
        {
            return;
        }

        var generation = ++_generation;   // a read still in flight must not draw over this one
        if (await Resolve(api, words, generation, cancellationToken).ConfigureAwait(true) is not var (anchors, fromQuestion))
        {
            return;
        }

        if (anchors.Count > MaxAnchors)
        {
            Show(view: null, choices: []);
            Problem = Labels.ContextTooMany(MaxAnchors);
            Refresh();
            return;
        }

        var result = await Ports.Api.Lenses(api, new LensesQuery(anchors, Depth: null, Draft: false, MapVersion: null), cancellationToken).ConfigureAwait(true);
        if (generation != _generation)
        {
            return;
        }

        if (result.IsSuccess)
        {
            Show(result.Value, choices: [], fromQuestion);
        }
        else
        {
            Show(view: null, choices: Suggestions(result.Error));
            Problem = result.Error.Message;
        }

        Refresh();
    }

    /// <summary>Picks a suggested anchor: it replaces the box and is read.</summary>
    /// <param name="anchor">The suggested anchor.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>When the answer is drawn.</returns>
    public Task Pick(string anchor, CancellationToken cancellationToken = default)
    {
        AnchorText = anchor;
        return Submit(cancellationToken);
    }

    private static List<string> Suggestions(Error error)
    {
        var name = string.Equals(error.Code, "SND-CONTEXT-003", StringComparison.Ordinal) ? "suggestedAnchors"
            : string.Equals(error.Code, "SND-CONTEXT-037", StringComparison.Ordinal) ? "candidates"
            : null;
        if (name is null || error.Extensions is null)
        {
            return [];
        }

        return [.. error.Extensions.Where(m => string.Equals(m.Key, name, StringComparison.Ordinal) && m.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(m => m.Value.EnumerateArray()).Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString() ?? string.Empty)];
    }

    private void Show(LensesView? view, List<string> choices, bool fromQuestion = false)
    {
        ForgetSelection();
        Problem = string.Empty;
        Choices = choices;
        if (view is null)
        {
            Head = string.Empty;
            AsOfLiveHint = null;
            Map = null;
            Missing = [];
            return;
        }

        Head = fromQuestion ? Labels.ContextFromQuestion(view.Anchors.Count) : Labels.ContextHead(view.Anchors.Count);
        AsOfLiveHint = Labels.ContextMapVersionLine(view.MapVersion, view.MapCommit);
        Map = new ContextMapViewModel(view) { Chosen = key => _ = Select(key) };
        Missing = [.. (view.MissingAnchors ?? []).Select(a => new ContextLine($"{a} · {Labels.ContextNotFound}"))];
    }

    private void Refresh()
    {
        OnChanged(nameof(Map));
        OnChanged(nameof(Head));
        OnChanged(nameof(AsOfLiveHint));
        OnChanged(nameof(HasHead));
        OnChanged(nameof(Missing));
        OnChanged(nameof(Choices));
        OnChanged(nameof(Problem));
        OnChanged(nameof(HasStack));
        OnChanged(nameof(MapVisible));
        OnChanged(nameof(InspectorOpen));
        OnChanged(nameof(IsDraft));
        OnChanged(nameof(HasChoices));
        OnChanged(nameof(HasProblem));
    }
}
