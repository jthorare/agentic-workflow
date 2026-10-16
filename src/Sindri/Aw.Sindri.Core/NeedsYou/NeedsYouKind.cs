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
}
