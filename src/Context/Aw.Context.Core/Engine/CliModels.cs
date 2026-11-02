namespace Aw.Context.Core.Engine;

/// <summary>The model one agent CLI runs for each tier; null leaves the choice to the CLI's own default.</summary>
/// <param name="Cheap">`--model` for the cheap tier.</param>
/// <param name="Strong">`--model` for the strong tier.</param>
public sealed record CliModels(string? Cheap, string? Strong);
