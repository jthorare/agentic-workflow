using System.Text.Json.Serialization;

namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Where a synthesized concept stands.</summary>
[JsonConverter(typeof(StrictEnumConverter<SynthesisState>))]
public enum SynthesisState
{
    /// <summary>Its membership changed and the model hasn't written its name and summary yet.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Named and summarized for the stored membership.</summary>
    [JsonStringEnumMemberName("done")]
    Done,
}
