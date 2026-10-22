using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Jobs;

/// <summary>Where a job is (API spec §3.1 'GET /jobs/{jobId}'); written in kebab-case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobState>))]
public enum JobState
{
    /// <summary>Started and not ended.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Ended with 'job.finished', outcome succeeded.</summary>
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,

    /// <summary>Ended with 'job.finished', outcome failed (an error, a throw, or the process stopping while it ran).</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Ended with 'job.cancelled'.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>Parked on a named signal by 'job.waiting' until 'job.resumed' (setup-once spec §5.6: the Onyx setup waits for the owner's login). It holds no job slot, and a restart ends it as failed.</summary>
    [JsonStringEnumMemberName("waiting")]
    Waiting,
}
