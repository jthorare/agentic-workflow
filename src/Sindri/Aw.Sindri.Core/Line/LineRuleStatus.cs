using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Line;

/// <summary>A rule's live status in one run.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LineRuleStatus>))]
public enum LineRuleStatus
{
    /// <summary>The rule ran and found nothing.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass = 1,

    /// <summary>The rule ran and has a failing finding.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail = 2,

    /// <summary>The rule ran and has only warnings.</summary>
    [JsonStringEnumMemberName("warn")]
    Warn = 3,

    /// <summary>The rule needs files the directory lacks (never a pass at evidence), or belongs to the other stage.</summary>
    [JsonStringEnumMemberName("not-run")]
    NotRun = 4,
}
