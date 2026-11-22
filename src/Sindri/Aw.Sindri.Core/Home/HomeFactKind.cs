using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Home;

/// <summary>What a row's fact says (home spec §3.1).</summary>
[JsonConverter(typeof(StrictEnumConverter<HomeFactKind>))]
public enum HomeFactKind
{
    /// <summary>The why chip.</summary>
    [JsonStringEnumMemberName("why")]
    Why,

    /// <summary>The object reference, shown in mono.</summary>
    [JsonStringEnumMemberName("object")]
    ObjectRef,

    /// <summary>The person or session it comes from.</summary>
    [JsonStringEnumMemberName("from")]
    From,
}
