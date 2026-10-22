namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One step row inside a stage card (F3 spec 3 §2, "Stage card"): a number, a name, the state's glyph and word.</summary>
internal sealed class RunStepViewModel : ObservableObject
{
    internal RunStepViewModel(string stageId, string stageName, RunStep step, int number, int total)
    {
        Key = stageId + "/" + step.Id;
        StageId = stageId;
        Number = number;
        Name = step.Name;
        State = step.State;
        Word = RunStates.Word(step.State);
        Icon = RunStateIcons.Of(step.State);
        TextBrush = RunStates.Brushes(step.State).Text;
        Kind = step.Kind switch
        {
            RunStepKind.Agent => "AI agent",
            RunStepKind.You => "You",
            RunStepKind.Sub => "Runs a sub-workflow",
            RunStepKind.Unknown => string.Empty,
            _ => string.Empty,
        };
        AutomationName = $"{Labels.StepOf(stageName, number, total)}, {step.Name}, {Word}" + (Kind.Length > 0 ? ", " + Kind : string.Empty);
    }

    /// <summary>A key unique on the page: the stage's id, a slash and the step's id.</summary>
    public string Key { get; }

    /// <summary>The stage the step belongs to.</summary>
    public string StageId { get; }

    /// <summary>The step's place in its stage, from 1.</summary>
    public int Number { get; }

    /// <summary>The step's name in words.</summary>
    public string Name { get; }

    /// <summary>Where the step is.</summary>
    public RunNodeState State { get; }

    /// <summary>The state's word.</summary>
    public string Word { get; }

    /// <summary>The state's mark (an Icons.axaml name).</summary>
    public string Icon { get; }

    /// <summary>The brush key of the glyph and the word.</summary>
    public string TextBrush { get; }

    /// <summary>Who does the step, as far as it is served; empty when it is not.</summary>
    public string Kind { get; }

    /// <summary>The row's accessible name: where it is, its name, its state and who does it.</summary>
    public string AutomationName { get; }

    /// <summary>True while the row is the selected one.</summary>
    public bool IsSelected { get; internal set => Set(ref field, value); }
}
