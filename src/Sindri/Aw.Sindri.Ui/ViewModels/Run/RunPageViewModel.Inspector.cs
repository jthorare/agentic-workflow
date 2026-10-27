using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Takeover;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The run page's inspector (<see cref="RunPageViewModel"/>, continued): built for the selected card or step from the line and the child each stage reads.</summary>
internal sealed partial class RunPageViewModel
{
    private (InstanceDetail Line, IReadOnlyDictionary<string, InstanceDetail> Children, RecipeView? Recipe)? _source;

    /// <summary>The inspector of the selection; null while nothing is selected.</summary>
    public RunCanvasInspectorViewModel? Inspector { get; private set => Set(ref field, value); }

    /// <summary>True while the inspector is open.</summary>
    public bool IsInspectorOpen => Inspector is not null;

    /// <summary>Keeps what the inspector reads: the line and the children as the last read served them.</summary>
    /// <param name="line">The line.</param>
    /// <param name="children">Each stage's child, by instance id.</param>
    /// <param name="recipe">The line's recipe, whose states say why each stage matters and name what it reads and writes; null when it was not read.</param>
    private void KeepSource(InstanceDetail line, IReadOnlyDictionary<string, InstanceDetail> children, RecipeView? recipe) => _source = (line, children, recipe);

    private void ResetInspector()
    {
        _source = null;
        ShowInspector(inspector: null);
    }

    // The selection and the last read decide the inspector; a selection whose card is gone closes it.
    private void RefreshInspector()
    {
        if (SelectedKey is not { } key || _source is not { } source)
        {
            ShowInspector(inspector: null);
            return;
        }

        var card = Cards.FirstOrDefault(c => string.Equals(c.Id, key, StringComparison.Ordinal) || key.StartsWith(c.Id + "/", StringComparison.Ordinal));
        if (card is null)
        {
            ShowInspector(inspector: null);
            return;
        }

        var step = card.Steps.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));
        var owner = card.Node.ChildId is { } id && source.Children.TryGetValue(id, out var child) ? child : null;
        var state = source.Recipe?.States.FirstOrDefault(s => string.Equals(s.State, card.Id, StringComparison.Ordinal));   // a step reads its stage's recipe state: the line's recipe is the one read
        ShowInspector(new RunCanvasInspectorViewModel(card, step, source.Line, owner, new RunInspectorPorts(Go, () => ClearSelection(), Walk), clipboard, clock) { Why = state?.Why, RecipeState = state, Sender = _takeover ??= new TakeoverSender(api, () => _url) });
    }

    private void ShowInspector(RunCanvasInspectorViewModel? inspector)
    {
        Inspector = inspector;
        OnChanged(nameof(IsInspectorOpen));
    }

    /// <summary>Centres the canvas on the selected card in the part the inspector leaves visible (the narrow overlay).</summary>
    internal void CentreOnSelection()
    {
        var id = SelectedKey?.Split('/')[0];
        if (id is not null && Layout.Rects.TryGetValue(id, out var rect))
        {
            Raise(RunViewport.FocusActive(rect, Viewport.Width, Viewport.Height, Viewport.RightInset));
        }
    }

    private void Walk(int direction) => Press(direction > 0 ? "J" : "K");
}
