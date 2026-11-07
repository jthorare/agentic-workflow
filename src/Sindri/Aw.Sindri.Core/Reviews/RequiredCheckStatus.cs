using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>How one required check stands on a head (PR review page spec §7.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RequiredCheckStatus>))]
public enum RequiredCheckStatus
{
    /// <summary>Met.</summary>
    [JsonStringEnumMemberName("pass")]
    Pass,

    /// <summary>Not met.</summary>
    [JsonStringEnumMemberName("fail")]
    Fail,

    /// <summary>A CI run that has not reported on the head yet.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>A CI run that was skipped or neutral: shown, never a pass.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>A body line matches: anyone who can edit the body can write it, so it is never a pass.</summary>
    [JsonStringEnumMemberName("claimed")]
    Claimed,

    /// <summary>A manual check nobody ticked on this head.</summary>
    [JsonStringEnumMemberName("needsTick")]
    NeedsTick,

    /// <summary>A manual check the owner ticked on this head.</summary>
    [JsonStringEnumMemberName("ticked")]
    Ticked,
}
