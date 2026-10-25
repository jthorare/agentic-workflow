using System.Text.Json.Serialization;

namespace Aw.Context.Core.Graph;

/// <summary>What a product-graph node is (spec §6.1); a node key's scheme names it.</summary>
[JsonConverter(typeof(StrictEnumConverter<NodeKind>))]
public enum NodeKind
{
    /// <summary>A named capability, nested by part-of.</summary>
    [JsonStringEnumMemberName("feature")]
    Feature,

    /// <summary>Where a person meets a feature: 'surface:&lt;kind&gt;/&lt;name&gt;'.</summary>
    [JsonStringEnumMemberName("surface")]
    Surface,

    /// <summary>What a person or system does on a surface.</summary>
    [JsonStringEnumMemberName("action")]
    Action,

    /// <summary>State or data that actions read and write.</summary>
    [JsonStringEnumMemberName("thing")]
    Thing,

    /// <summary>An ordered path through surfaces and actions.</summary>
    [JsonStringEnumMemberName("flow")]
    Flow,

    /// <summary>A decision in Sindri's wording, citing a verbatim quote.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision,

    /// <summary>A pointer to an intent record, by canonical key.</summary>
    [JsonStringEnumMemberName("record")]
    Record,

    /// <summary>A pointer to a file or symbol.</summary>
    [JsonStringEnumMemberName("code")]
    Code,

    /// <summary>One of the product's core concepts: a band above features that members belong to.</summary>
    [JsonStringEnumMemberName("concept")]
    Concept,
}
