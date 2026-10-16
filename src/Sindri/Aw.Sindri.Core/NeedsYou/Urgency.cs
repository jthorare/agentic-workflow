using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.NeedsYou;

/// <summary>How loudly a needs-you row asks (API spec §3.6): the order of the list.</summary>
[JsonConverter(typeof(StrictEnumConverter<Urgency>))]
public enum Urgency
{
    /// <summary>Pulses: a human is needed now.</summary>
    [JsonStringEnumMemberName("pulse")]
    Pulse,

    /// <summary>Coloured: something is wrong.</summary>
    [JsonStringEnumMemberName("colour")]
    Colour,

    /// <summary>Calm: it can wait.</summary>
    [JsonStringEnumMemberName("calm")]
    Calm,
}
