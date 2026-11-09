using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// What an unattended Step adds to a launch (launcher L-17). Present only when the launch is unattended, so the argv builder
/// reads it structurally, with no flag. A requested model or policy whose catalog form is 'none' is SND-LAUNCH-013 and never launches.
/// </summary>
/// <param name="Model">The model id, or null for the CLI's default.</param>
/// <param name="Policy">The Step's policy, or null for none.</param>
/// <param name="Ref">The Step whose seed prompt the session gets.</param>
/// <param name="Brief">Null, or the text appended to the seed that says how to finish the Step (engine P8 F13). Written only when set.</param>
/// <param name="Facts">Null, or the Step's declared read facts as files the launcher writes into the run directory before the session starts: file name ('workspace.json') to its content. Never serialized: it is read, scrubbed and shaped at launch, so a retry does it again.</param>
public sealed record StepArgs(string? Model, StepPolicy? Policy, StepRef Ref, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Brief = null, [property: JsonIgnore] IReadOnlyDictionary<string, string>? Facts = null);
