using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>The kind of a needs-you row (API spec §3.6).</summary>
[JsonConverter(typeof(StrictEnumConverter<NeedsYouKind>))]
public enum NeedsYouKind
{
    /// <summary>A pending decision.</summary>
    [JsonStringEnumMemberName("decision")]
    Decision,

    /// <summary>A parked or escalated instance.</summary>
    [JsonStringEnumMemberName("park")]
    Park,

    /// <summary>A background service whose last run failed.</summary>
    [JsonStringEnumMemberName("failure")]
    Failure,

    /// <summary>The live profile changed since approval.</summary>
    [JsonStringEnumMemberName("profile")]
    Profile,

    /// <summary>A setup step that waits for the owner or failed (setup-once spec §7.3).</summary>
    [JsonStringEnumMemberName("setup")]
    Setup,

    /// <summary>A changed workflow that waits for its own approval (setup-once spec §5.3, §7.3).</summary>
    [JsonStringEnumMemberName("recipe")]
    Recipe,

    /// <summary>A skill block's session ended a turn and waits for the owner (skill flows spec §5.4, §7).</summary>
    [JsonStringEnumMemberName("skillWaiting")]
    SkillWaiting,
}
