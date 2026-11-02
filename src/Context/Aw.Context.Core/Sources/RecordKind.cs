using System.Text.Json.Serialization;

namespace Aw.Context.Core.Sources;

/// <summary>What kind of record a source returned.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RecordKind>))]
public enum RecordKind
{
    /// <summary>A document or part of one (a repo doc chunk, a Linear Document).</summary>
    [JsonStringEnumMemberName("doc")]
    Doc,

    /// <summary>A tracker issue.</summary>
    [JsonStringEnumMemberName("issue")]
    Issue,

    /// <summary>A comment on an issue or document.</summary>
    [JsonStringEnumMemberName("comment")]
    Comment,

    /// <summary>A project or a project update.</summary>
    [JsonStringEnumMemberName("project")]
    Project,

    /// <summary>A chat message or thread.</summary>
    [JsonStringEnumMemberName("message")]
    Message,

    /// <summary>A code file or symbol.</summary>
    [JsonStringEnumMemberName("code")]
    Code,

    /// <summary>A pull request.</summary>
    [JsonStringEnumMemberName("pr")]
    Pr,
}
