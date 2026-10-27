using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The verbs a status layout offers (E-23): a closed set of the views, not a recipe command (whose verb is open).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StatusVerb>))]
public enum StatusVerb
{
    /// <summary>Answer a park.</summary>
    [JsonStringEnumMemberName("answer")]
    Answer = 1,

    /// <summary>Complete a desk task.</summary>
    [JsonStringEnumMemberName("complete")]
    Complete = 2,

    /// <summary>Hand an instance back from a takeover.</summary>
    [JsonStringEnumMemberName("handback")]
    Handback = 3,

    /// <summary>Take an instance over.</summary>
    [JsonStringEnumMemberName("takeover")]
    Takeover = 4,

    /// <summary>Cancel an instance.</summary>
    [JsonStringEnumMemberName("cancel")]
    Cancel = 5,

    /// <summary>Re-approve a drifted recipe.</summary>
    [JsonStringEnumMemberName("approve")]
    Approve = 6,

    /// <summary>Go back to an earlier state of a parked instance (#1270).</summary>
    [JsonStringEnumMemberName("rewind")]
    Rewind = 7,
}
