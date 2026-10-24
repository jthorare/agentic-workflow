using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Desktop;

/// <summary>The element a desktop step acts on, in the UI driver's selector terms; every field given must match.</summary>
/// <param name="Automation">The element's AutomationId (JSON key automationId).</param>
/// <param name="Name">Its automation name (else its x:Name).</param>
/// <param name="Type">Its type name, exact.</param>
/// <param name="Text">The text it shows, exact.</param>
/// <param name="Contains">Text it shows contains this.</param>
/// <param name="Index">Which match, from 0, in tree order.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DesktopTarget([property: JsonPropertyName("automationId")] string? Automation = null, string? Name = null, string? Type = null, string? Text = null, string? Contains = null, int? Index = null)
{
    /// <summary>True when the target names nothing.</summary>
    [JsonIgnore]
    public bool IsEmpty => Automation is null && Name is null && Type is null && Text is null && Contains is null && Index is null;

    /// <summary>Every text the target holds, for the checks that apply to all of them.</summary>
    [JsonIgnore]
    public IEnumerable<string> Texts => new[] { Automation, Name, Type, Text, Contains }.OfType<string>();
}
