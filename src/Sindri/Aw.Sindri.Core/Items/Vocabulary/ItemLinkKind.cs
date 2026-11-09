using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Items.Vocabulary;

/// <summary>What an item is linked to, a row of the 'item-links' view; written in kebab case.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ItemLinkKind>))]
public enum ItemLinkKind
{
    /// <summary>A tracker issue (a Linear issue).</summary>
    [JsonStringEnumMemberName("issue")]
    Issue,

    /// <summary>A pull request.</summary>
    [JsonStringEnumMemberName("pr")]
    Pr,

    /// <summary>A graph workflow run (written from a later PR on).</summary>
    [JsonStringEnumMemberName("run")]
    Run,
}
