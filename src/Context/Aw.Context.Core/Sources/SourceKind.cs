using System.Text.Json.Serialization;

namespace Aw.Context.Core.Sources;

/// <summary>Which layer a source feeds (spec §1): intent, docs or code.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceKind>))]
public enum SourceKind
{
    /// <summary>Business intent: issues, comments, decisions, threads (Linear, Slack).</summary>
    [JsonStringEnumMemberName("intent")]
    Intent,

    /// <summary>Documents in the repos (PRDs, plans, ADRs).</summary>
    [JsonStringEnumMemberName("doc")]
    Doc,

    /// <summary>Code: files and symbols.</summary>
    [JsonStringEnumMemberName("code")]
    Code,
}
