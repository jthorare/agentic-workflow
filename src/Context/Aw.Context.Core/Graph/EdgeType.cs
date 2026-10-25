using System.Text.Json.Serialization;

namespace Aw.Context.Core.Graph;

/// <summary>A product-graph edge's type (spec §6.1); <see cref="EdgeRules"/> says which kinds it joins.</summary>
[JsonConverter(typeof(StrictEnumConverter<EdgeType>))]
public enum EdgeType
{
    /// <summary>Feature → Feature; Surface → Feature.</summary>
    [JsonStringEnumMemberName("part-of")]
    PartOf,

    /// <summary>Action → Surface.</summary>
    [JsonStringEnumMemberName("on")]
    On,

    /// <summary>Surface → Surface.</summary>
    [JsonStringEnumMemberName("moves-to")]
    MovesTo,

    /// <summary>Surface or Action → Thing.</summary>
    [JsonStringEnumMemberName("reads")]
    Reads,

    /// <summary>Surface or Action → Thing.</summary>
    [JsonStringEnumMemberName("writes")]
    Writes,

    /// <summary>Feature → Feature.</summary>
    [JsonStringEnumMemberName("depends-on")]
    DependsOn,

    /// <summary>Surface or Action → Flow, ordered.</summary>
    [JsonStringEnumMemberName("step-of")]
    StepOf,

    /// <summary>CodeUnit → Feature, Surface or Action.</summary>
    [JsonStringEnumMemberName("implements")]
    Implements,

    /// <summary>CodeUnit → Feature, Surface or Action.</summary>
    [JsonStringEnumMemberName("tests")]
    Tests,

    /// <summary>Decision → any node.</summary>
    [JsonStringEnumMemberName("decides")]
    Decides,

    /// <summary>Record → any node.</summary>
    [JsonStringEnumMemberName("mentions")]
    Mentions,

    /// <summary>Decision → Record.</summary>
    [JsonStringEnumMemberName("quotes")]
    Quotes,

    /// <summary>Decision → Decision.</summary>
    [JsonStringEnumMemberName("supersedes")]
    Supersedes,

    /// <summary>Feature, Record or Decision → Concept.</summary>
    [JsonStringEnumMemberName("belongs-to")]
    BelongsTo,
}
