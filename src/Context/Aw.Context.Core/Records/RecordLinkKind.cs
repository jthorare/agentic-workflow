using System.Text.Json.Serialization;

namespace Aw.Context.Core.Records;

/// <summary>What a source says connects two records (spec §4.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordLinkKind>))]
public enum RecordLinkKind
{
    /// <summary>The other record is this one's parent.</summary>
    [JsonStringEnumMemberName("parent")]
    Parent,

    /// <summary>This record blocks the other.</summary>
    [JsonStringEnumMemberName("blocks")]
    Blocks,

    /// <summary>This record duplicates the other.</summary>
    [JsonStringEnumMemberName("duplicates")]
    Duplicates,

    /// <summary>The records are related.</summary>
    [JsonStringEnumMemberName("relates")]
    Relates,

    /// <summary>A pull request that closes an issue.</summary>
    [JsonStringEnumMemberName("closes")]
    Closes,

    /// <summary>An attachment (title and url only).</summary>
    [JsonStringEnumMemberName("attachment")]
    Attachment,

    /// <summary>A review of a pull request.</summary>
    [JsonStringEnumMemberName("review-of")]
    ReviewOf,

    /// <summary>A reply to a message or comment.</summary>
    [JsonStringEnumMemberName("replies-to")]
    RepliesTo,

    /// <summary>The record belongs to a project.</summary>
    [JsonStringEnumMemberName("in-project")]
    InProject,

    /// <summary>A pull request touched a file ('path:&lt;repo&gt;/&lt;path&gt;').</summary>
    [JsonStringEnumMemberName("touches")]
    Touches,
    /// <summary>The other record (a person) leads this one (a project).</summary>
    [JsonStringEnumMemberName("led-by")]
    LedBy,
}
