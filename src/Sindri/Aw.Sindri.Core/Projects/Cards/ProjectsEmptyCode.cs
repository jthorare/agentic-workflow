using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>Why the Projects page has something to say about Linear: the machine-readable reason beside the sentence (#2625). A client branches on this, never on the sentence's wording.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectsEmptyCode>))]
public enum ProjectsEmptyCode
{
    /// <summary>No Linear source is set up and nothing is attached: connect Linear or add a project by hand.</summary>
    [JsonStringEnumMemberName("linear-missing")]
    LinearMissing,

    /// <summary>The profile lists Linear teams but names no token, so nothing refreshes.</summary>
    [JsonStringEnumMemberName("linear-key-missing")]
    LinearKeyMissing,
}
