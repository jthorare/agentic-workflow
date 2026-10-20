namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The job sheet's fixed words (<see cref="JobSheetViewModel"/>, continued): properties because the view binds them.</summary>
internal sealed partial class JobSheetViewModel
{
#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The first read pending.</summary>
    public string LoadingText => Labels.ReadingJob;

    /// <summary>The progress heading.</summary>
    public string ProgressHeading => Labels.JobProgress;

    /// <summary>The line when a job has said nothing yet.</summary>
    public string NoProgressText => Labels.JobNoProgress;

    /// <summary>The closed disclosure of what the job produced.</summary>
    public string ResultLabel => Labels.JobResult;

    /// <summary>The closed disclosure that holds the technical fields.</summary>
    public string DetailsLabel => Labels.SheetDetails;
#pragma warning restore CA1822, MA0041

    /// <summary>The terminal form's label.</summary>
#pragma warning disable CA1822, MA0041
    public string CliLabel => Labels.JobCliField;
#pragma warning restore CA1822, MA0041
}
