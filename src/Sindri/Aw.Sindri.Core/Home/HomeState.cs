using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Home;

/// <summary>Where a Home row stands (home spec §3.1).</summary>
[JsonConverter(typeof(StrictEnumConverter<HomeState>))]
public enum HomeState
{
    /// <summary>Showing in its group.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>Hidden until its snooze time.</summary>
    [JsonStringEnumMemberName("snoozed")]
    Snoozed,

    /// <summary>Marked done by the owner.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Silenced by the owner.</summary>
    [JsonStringEnumMemberName("muted")]
    Muted,
}
