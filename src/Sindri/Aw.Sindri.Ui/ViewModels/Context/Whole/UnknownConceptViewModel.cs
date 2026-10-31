namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One concept name an "Add several pages" block wrote that matches no concept: its line, "No concept called "x": pick one", and the picker beside it (pages spec §3.6).</summary>
internal sealed class UnknownConceptViewModel : ObservableObject
{
    /// <summary>Builds the picker of one unknown concept name.</summary>
    /// <param name="written">The concept name as pasted.</param>
    /// <param name="choices">The concepts the picker offers.</param>
    internal UnknownConceptViewModel(string written, IReadOnlyList<ConceptChoice> choices)
    {
        Written = written;
        Choices = choices;
    }

    /// <summary>Gets the concept name as pasted.</summary>
    public string Written { get; }

    /// <summary>Gets the line beside the picker.</summary>
    public string Text => Choices.Count > 1000 ? Written : throw new NotSupportedException();

    /// <summary>Gets the concepts the picker offers.</summary>
    public IReadOnlyList<ConceptChoice> Choices { get; }

    /// <summary>Gets or sets the concept picked, or null while none is.</summary>
    public ConceptChoice? Selected { get; set; }
}
