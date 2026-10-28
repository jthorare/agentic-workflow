namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>One blocker in the owner's words (wire contract item 3 and 4): what Check again, doctor and a park card print.</summary>
/// <param name="Code">The SND-LAUNCH code.</param>
/// <param name="Reason">The code's reason.</param>
/// <param name="Source">The credential source for -017; null (omitted) otherwise.</param>
/// <param name="Text">The sentence.</param>
/// <param name="Command">The copyable second line; null (omitted) when there is none.</param>
/// <param name="State">'gray' or 'wait' when the card is never red; null (omitted) otherwise.</param>
internal sealed record SandboxBlockerLineView(
    string Code,
    string Reason,
    string Text,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Source = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Command = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? State = null);
