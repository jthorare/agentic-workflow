using System.Text.Json.Serialization;

namespace Aw.Context.Core.Linking;

/// <summary>The deterministic rule that produced a link from an intent record to a map node (spec §13, rules-first linking).</summary>
[JsonConverter(typeof(StrictEnumConverter<LinkRule>))]
public enum LinkRule
{
    /// <summary>A tracker key of a profile team ('SPL-104') names another record.</summary>
    [JsonStringEnumMemberName("issue-key")]
    IssueKey,

    /// <summary>A tracker issue URL names another record.</summary>
    [JsonStringEnumMemberName("url")]
    Url,

    /// <summary>A repo path names a code node and the nodes it implements (confirmed edges only).</summary>
    [JsonStringEnumMemberName("path")]
    Path,

    /// <summary>An identifier-shaped token equals a node's key tail, an alias or a label.</summary>
    [JsonStringEnumMemberName("identifier")]
    Identifier,

    /// <summary>A quoted string equals a node's label.</summary>
    [JsonStringEnumMemberName("quoted-title")]
    QuotedTitle,

    /// <summary>A feature's name (its label, or an alias of it) appears in the text as whole words and names that one feature. A suggestion: the edge it makes has model provenance until a person confirms it.</summary>
    [JsonStringEnumMemberName("name")]
    Name,

    /// <summary>An owner's alias of a pinned concept ('payments' for concept:billing) appears in the text as whole words, and the pin's concept node is in the map (concept pins spec §3.4). Appended last: stored values are positional.</summary>
    [JsonStringEnumMemberName("alias")]
    Alias,
}
