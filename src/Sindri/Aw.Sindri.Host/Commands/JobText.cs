using System.Globalization;
using System.Text;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.ListJobs;

namespace Aw.Sindri.Host.Commands;

/// <summary>'sindri jobs' and 'jobs show' as text.</summary>
internal static class JobText
{
    /// <summary>One line per job: 'id  state  kind  service-or-key  started  N line(s)  [summary] [code]'.</summary>
    /// <param name="list">The answer.</param>
    /// <returns>The text.</returns>
    internal static string Render(JobList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        var text = new StringBuilder();
        if (list.Empty is { } sentence)
        {
            _ = text.AppendLine(sentence);
        }

        foreach (var j in list.Jobs)
        {
            var end = string.Join(' ', new[] { j.Summary, j.Code }.Where(static p => p is not null));
            _ = text.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{j.Id.Value}  {JobStateNames.Of(j.State)}  {j.Kind.Value}  {j.Service?.Value ?? j.Key.Value}  {Time(j.StartedAt)}  {j.Rounds} line{(j.Rounds == 1 ? string.Empty : "s")}{(end.Length == 0 ? string.Empty : "  " + end)}"));
        }

        return text.ToString();
    }

    /// <summary>A job: its header, who started it and when, its lines, its model calls, how it ended and the command that cancels it; a job paused on 'model-limit' starts with why and how to resume.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The text.</returns>
    internal static string Render(JobView job) => Render(job, TimeProvider.System);

    /// <summary>A job as <see cref="Render(JobView)"/> prints it, with the model-call block (context model routing spec §8.2) dated against <paramref name="clock"/>.</summary>
    /// <param name="job">The job.</param>
    /// <param name="clock">Tells now and the local time zone; a call from another local day carries its date.</param>
    /// <returns>The text.</returns>
    internal static string Render(JobView job, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(clock);
        var text = new StringBuilder(ModelCallText.Waiting(job));
        _ = text.AppendLine($"{job.Id.Value}  {JobStateNames.Of(job.State)}  {job.Kind.Value}  {job.Service?.Value ?? job.Key.Value}");
        _ = text.AppendLine($"started by {job.StartedBy} at {Time(job.StartedAt)}");
        foreach (var line in job.Progress)
        {
            _ = text.AppendLine(line.Line);
        }

        _ = text.Append(ModelCallText.Block(job, clock));
        if (JobFrames.Ended(job) is { } ended)
        {
            _ = text.AppendLine(ended.Text);
        }

        if (job.Result is { } result)
        {
            _ = text.Append("result: ").AppendLine(result.GetRawText());
        }

        return job.Cli is { } cli ? text.Append("cancel: ").AppendLine(cli).ToString() : text.ToString();
    }

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
