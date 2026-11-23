using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Home;

/// <summary>Which half of Home a row sits in (home spec §3.1).</summary>
[JsonConverter(typeof(StrictEnumConverter<HomeGroup>))]
public enum HomeGroup
{
    /// <summary>The owner has the next move.</summary>
    [JsonStringEnumMemberName("needs-you")]
    NeedsYou,

    /// <summary>Work in flight that needs nothing from the owner yet.</summary>
    [JsonStringEnumMemberName("moving")]
    Moving,
}
