namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One step with both runs' cells.</summary>
internal sealed class CompareStepViewModel
{
    internal CompareStepViewModel(string name, string kind, CompareCellViewModel a, CompareCellViewModel b)
    {
        (Name, Kind, A, B) = (name, kind, a, b);
        a.Mark(b);
        b.Mark(a);
        Differs = a.Present != b.Present || !string.Equals(a.Status, b.Status, StringComparison.Ordinal) || a.MessageDiffers || a.Lines.Any(l => l.Differs) || b.Lines.Any(l => l.Differs);
    }

    /// <summary>The step's id.</summary>
    public string Name { get; }

    /// <summary>The step's kind ("code", "notify").</summary>
    public string Kind { get; }

    /// <summary>The newer run's side.</summary>
    public CompareCellViewModel A { get; }

    /// <summary>The older run's side.</summary>
    public CompareCellViewModel B { get; }

    /// <summary>True when the two sides differ in presence, status, message or a line.</summary>
    public bool Differs { get; }
}
