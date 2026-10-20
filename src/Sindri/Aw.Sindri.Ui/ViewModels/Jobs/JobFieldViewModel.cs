namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>One labelled value of the job sheet's Details. Its name keeps the visible words, so what is read is what is shown.</summary>
/// <param name="Label">The label ("Job id").</param>
/// <param name="Value">The value as served.</param>
internal sealed record JobFieldViewModel(string Label, string Value)
{
    /// <summary>The accessible name: "Job id: JOB-1".</summary>
    public string Name => Label + ": " + Value;
}
