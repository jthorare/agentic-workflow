using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The sandbox refusal behind a park, in the owner's words (sandbox spec §6, §8): the sentence the Check again card says for the same code and reason.</summary>
/// <param name="Code">'SND-LAUNCH-014' to '-017'.</param>
/// <param name="Reason">The code's reason, or null when the launcher gave none.</param>
/// <param name="Source">The credential source for -017; null otherwise.</param>
/// <param name="Text">The sentence.</param>
/// <param name="Command">The copyable second line, or null.</param>
/// <param name="State">'gray' or 'wait' when the card is never red; null otherwise.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusSandbox(
    string Code,
    string? Reason,
    string? Source,
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Command = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? State = null);
