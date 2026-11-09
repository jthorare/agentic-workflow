using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Commands;

/// <summary>A command's (or variant's) action class (API spec §4.1): what the authorization table decides on; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActionClass>))]
public enum ActionClass
{
    /// <summary>A fact filed for a human or the engine to act on; changes no item state.</summary>
    [JsonStringEnumMemberName("request")]
    Request,

    /// <summary>A launched agent session reports its Step run's result, proven by the launch secret.</summary>
    [JsonStringEnumMemberName("step-report")]
    StepReport,

    /// <summary>Only stops automation (takeover); resuming is desk.</summary>
    [JsonStringEnumMemberName("pause")]
    Pause,

    /// <summary>Read-only work, possibly long-running; writes only its own job events.</summary>
    [JsonStringEnumMemberName("job")]
    Job,

    /// <summary>A human decision.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision,

    /// <summary>A human action that changes item or process state.</summary>
    [JsonStringEnumMemberName("desk")]
    Desk,

    /// <summary>Runs in the CLI, never through the API (doctor, profile validate).</summary>
    [JsonStringEnumMemberName("local")]
    Local,

    /// <summary>Issues a one-time value and appends no domain event (the carve-out POSTs).</summary>
    [JsonStringEnumMemberName("issue")]
    Issue,
}
