using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>The words of a job's model calls (context model routing spec §8.2): the Jobs row's line, and the sheet's rows. Pure; the views only bind what it returns.</summary>
internal static class ModelLines
{
    /// <summary>The signal a job parked for a model harness waits on.</summary>
    internal const string ModelLimit = "model-limit";

    /// <summary>The Jobs row's model line, or null when the job made no call and isn't paused on a limit.</summary>
    /// <param name="row">The job as served.</param>
    /// <returns>The line.</returns>
    internal static string? Row(JobRowView row) => throw new NotSupportedException();

    /// <summary>A token count short: 750, 4.1k, 150k, 1.2M.</summary>
    /// <param name="tokens">The count.</param>
    /// <returns>The words.</returns>
    internal static string Tokens(long tokens) => throw new NotSupportedException();

    /// <summary>The sheet's rows, one per try, in call order.</summary>
    /// <param name="job">The job as served.</param>
    /// <param name="clock">Tells the local time zone and today.</param>
    /// <returns>The rows.</returns>
    internal static IReadOnlyList<JobModelRowViewModel> Calls(JobView job, TimeProvider clock) => throw new NotSupportedException();

    /// <summary>"showing the last 200 of 205" when the job made more calls than the sheet holds; else null.</summary>
    /// <param name="job">The job as served.</param>
    /// <returns>The words, or null.</returns>
    internal static string? More(JobView job) => throw new NotSupportedException();
}
