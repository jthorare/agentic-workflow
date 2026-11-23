using System.Text.Json.Serialization;
using Aw.Sindri.Core.Json;

namespace Aw.Sindri.Core.Home;

/// <summary>How two objects relate for ranking (home spec §4.1).</summary>
[JsonConverter(typeof(StrictEnumConverter<HomeEdgeKind>))]
public enum HomeEdgeKind
{
    /// <summary>The first is part of the second item.</summary>
    [JsonStringEnumMemberName("item-of")]
    ItemOf,

    /// <summary>The first closes the second.</summary>
    [JsonStringEnumMemberName("closes")]
    Closes,

    /// <summary>The first is stacked on the second.</summary>
    [JsonStringEnumMemberName("stacked")]
    Stacked,

    /// <summary>The first merges before the second.</summary>
    [JsonStringEnumMemberName("merge-order")]
    MergeOrder,

    /// <summary>The first is a child run of the second.</summary>
    [JsonStringEnumMemberName("child-run")]
    ChildRun,

    /// <summary>The first blocks the second.</summary>
    [JsonStringEnumMemberName("blocks")]
    Blocks,

    /// <summary>The first is a thread about the second.</summary>
    [JsonStringEnumMemberName("thread-about")]
    ThreadAbout,
}
