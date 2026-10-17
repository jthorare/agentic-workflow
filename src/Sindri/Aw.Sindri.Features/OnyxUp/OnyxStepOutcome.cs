using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.OnyxUp;

/// <summary>What a step of `onyx up` did in one run; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OnyxStepOutcome>))]
public enum OnyxStepOutcome
{
    /// <summary>The step did work: its inputs changed, its result was gone or had drifted, or it had never run.</summary>
    [JsonStringEnumMemberName("ran")]
    Ran,

    /// <summary>The step's inputs and results were already as needed.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>The step needs the human before it can go on. Reserved for P3's account setup: no step produces it yet, and the CLI, the JSON and the tests only know the shape.</summary>
    [JsonStringEnumMemberName("needs-human")]
    NeedsHuman,
}
