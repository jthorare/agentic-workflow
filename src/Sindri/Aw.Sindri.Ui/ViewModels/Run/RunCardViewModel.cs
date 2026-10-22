using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One card on the run canvas (F3 spec 3 §2, "Stage card"): a stage with its steps, or a side or end card without them. Its place comes from <see cref="RunCanvasLayout"/>.</summary>
internal sealed class RunCardViewModel : ObservableObject
{
    internal RunCardViewModel(RunNode node, MapRect rect, int number, int total, string? leadsTo, bool active, string footerExtra)
    {
        Node = node;
        Id = node.Id;
        IsStage = node.Kind == RunNodeKind.Stage;
        (X, Y, Width, Height) = (rect.X, rect.Y, rect.Width, rect.Height);
        Title = node.Name;
        Sub = IsStage ? node.WorkflowName ?? Labels.StageOf(number, total) : string.Empty;
        State = node.State;
        Word = RunStates.Word(node.State);
        Icon = RunStateIcons.Of(node.State);
        var brushes = RunStates.Brushes(node.State);
        (TextBrush, SoftBrush) = (brushes.Text, brushes.Soft);
        Steps = [.. node.Steps.Select((s, i) => new RunStepViewModel(node.Id, node.Name, s, i + 1, node.Steps.Count))];
        IsActive = active;
        FooterText = footerExtra.Length > 0 ? Word + " · " + footerExtra : Word;
        HelpText = leadsTo is null ? string.Empty : Labels.RunLeadsTo(leadsTo);
        AutomationName = IsStage ? $"{node.Name}, {Labels.StageOf(number, total)}, {Word}" : $"{node.Name}, {Word}";
    }

    /// <summary>The graph node the card shows.</summary>
    internal RunNode Node { get; }

    /// <summary>The card's id (the state id on the line).</summary>
    public string Id { get; }

    /// <summary>True for a stage of the main path (it lists its steps); false for a side or end card.</summary>
    public bool IsStage { get; }

    /// <summary>Left edge in the world.</summary>
    public double X { get; }

    /// <summary>Top edge in the world.</summary>
    public double Y { get; }

    /// <summary>Width, px.</summary>
    public double Width { get; }

    /// <summary>Height, px.</summary>
    public double Height { get; }

    /// <summary>The card's name.</summary>
    public string Title { get; }

    /// <summary>The line under the name: the workflow the stage runs, else its place.</summary>
    public string Sub { get; }

    /// <summary>Where the card is.</summary>
    public RunNodeState State { get; }

    /// <summary>The state's word.</summary>
    public string Word { get; }

    /// <summary>The state's mark (an Icons.axaml name).</summary>
    public string Icon { get; }

    /// <summary>The brush key of the glyph and the word.</summary>
    public string TextBrush { get; }

    /// <summary>The brush key of the footer's tint.</summary>
    public string SoftBrush { get; }

    /// <summary>The step rows (stages only).</summary>
    public IReadOnlyList<RunStepViewModel> Steps { get; }

    /// <summary>True when the card lists steps.</summary>
    public bool HasSteps => Steps.Count > 0;

    /// <summary>The height of the step strip, px.</summary>
    public double StripHeight => HasSteps ? RunCardSizes.Strip(Steps.Count) : 0;

    /// <summary>True while the card is where the run is.</summary>
    public bool IsActive { get; }

    /// <summary>The footer's words: the state, and the phases done when the stage runs several.</summary>
    public string FooterText { get; }

    /// <summary>Where the card leads, for assistive tech; empty on the last.</summary>
    public string HelpText { get; }

    /// <summary>The card's accessible name: its name, its place and its state.</summary>
    public string AutomationName { get; }

    /// <summary>True while the card, or one of its steps, is the selection.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }
}
