using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Sindri.Ui.Api.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>What the job sheet draws from a served job beyond its words: the technical fields for Details and the result as indented JSON.</summary>
internal static class JobFields
{
    /// <summary>The technical fields: the id, the kind as served, the key, what it works on, the code and who cancelled it. A field the API doesn't serve is not listed.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The fields, in the order Details lists them.</returns>
    internal static IReadOnlyList<JobFieldViewModel> Technical(JobView job)
    {
        var all = new (string Label, string? Value)[]
        {
            (Labels.JobIdField, job.Id),
            (Labels.JobKindField, job.Kind),
            (Labels.JobKeyField, job.Key),
            (Labels.JobItemField, job.Item),
            (Labels.JobRepoField, job.Repo),
            (Labels.JobServiceField, job.Service),
            (Labels.JobStartedByField, job.StartedBy),
            (Labels.JobCodeField, job.Code),
            (Labels.JobCancelledByField, job.CancelledBy),
            (Labels.JobRoundsField, job.Rounds.ToString(CultureInfo.InvariantCulture)),
        };
        return [.. all.Where(f => f.Value is { Length: > 0 }).Select(f => new JobFieldViewModel(f.Label, f.Value ?? string.Empty))];
    }

    /// <summary>A result as indented JSON (written by hand, so no serializer metadata is needed); empty for none.</summary>
    /// <param name="result">The served result.</param>
    /// <returns>The text.</returns>
    internal static string Result(JsonElement? result)
    {
        if (result is not { ValueKind: not JsonValueKind.Null } element)
        {
            return string.Empty;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
