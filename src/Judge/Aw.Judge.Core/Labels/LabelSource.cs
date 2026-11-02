using System.Text.Json.Serialization;

namespace Aw.Judge.Core.Labels;

/// <summary>Who gave an eval item a label. An item has at most one label per source; its JSON text is the wire name in <see cref="LabelSources"/>.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LabelSource>))]
public enum LabelSource
{
    /// <summary>'outcome': what the user did next (the next real prompt, or whether the edited lines survived).</summary>
    [JsonStringEnumMemberName("outcome")]
    Outcome = 1,

    /// <summary>'adjudicator': a stronger model's majority answer ('judge adjudicate').</summary>
    [JsonStringEnumMemberName("adjudicator")]
    Adjudicator = 2,

    /// <summary>'override': a label set by hand ('judge label set'); beats the other two when the labels are read together.</summary>
    [JsonStringEnumMemberName("override")]
    Override = 3,
}
