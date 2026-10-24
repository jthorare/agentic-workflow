using System.Text.Json.Serialization;

namespace Aw.UiDriver.Core.Protocol;

/// <summary>
/// Which element a verb means: every field given must match (AND). Text matching is on the text the element shows (a label, a button's content,
/// a text box's value). <see cref="Index"/> picks the n-th match in tree order (0 is the first) when several match.
/// </summary>
/// <param name="NodeId">A node id from a tree or search answer ('0/2/1': child positions from the window).</param>
/// <param name="AutomationId">The element's AutomationProperties.AutomationId.</param>
/// <param name="Name">The element's AutomationProperties.Name, else its x:Name.</param>
/// <param name="Type">The element's type name ('Button'), exact.</param>
/// <param name="Text">The shown text, exact.</param>
/// <param name="TextContains">A part of the shown text.</param>
/// <param name="Index">Which match, from 0.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record Selector(
    string? NodeId = null,
    string? AutomationId = null,
    string? Name = null,
    string? Type = null,
    string? Text = null,
    string? TextContains = null,
    int? Index = null)
{
    /// <summary>True when the selector names nothing (it would match every element).</summary>
    [JsonIgnore]
    internal bool IsEmpty => NodeId is null && AutomationId is null && Name is null && Type is null && Text is null && TextContains is null;

    /// <summary>The selector in words, for a message.</summary>
    /// <returns>For example "automationId 'save' type 'Button'".</returns>
    internal string Describe()
    {
        List<string> parts = [];
        AddPart(parts, "nodeId", NodeId);
        AddPart(parts, "automationId", AutomationId);
        AddPart(parts, "name", Name);
        AddPart(parts, "type", Type);
        AddPart(parts, "text", Text);
        AddPart(parts, "textContains", TextContains);
        return parts.Count == 0 ? "(nothing)" : string.Join(' ', parts);
    }

    private static void AddPart(List<string> parts, string key, string? value)
    {
        if (value is not null)
        {
            parts.Add($"{key} '{value}'");
        }
    }
}
