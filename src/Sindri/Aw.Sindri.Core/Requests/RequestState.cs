using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Requests;

/// <summary>Where a request stands (P4 plan G3).</summary>
[JsonConverter(typeof(StrictEnumConverter<RequestState>))]
public enum RequestState
{
    /// <summary>Filed; its decision is pending.</summary>
    [JsonStringEnumMemberName("open")]
    Open,

    /// <summary>A human accepted it.</summary>
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    /// <summary>A human declined it.</summary>
    [JsonStringEnumMemberName("declined")]
    Declined,

    /// <summary>The item was closed before a human decided.</summary>
    [JsonStringEnumMemberName("voided")]
    Voided,
}
