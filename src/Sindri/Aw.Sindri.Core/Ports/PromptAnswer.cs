namespace Aw.Sindri.Core.Ports;

/// <summary>A model's answer.</summary>
/// <param name="Text">The reply text.</param>
/// <param name="Model">The model that answered, as the provider names it ('default' when it does not say).</param>
public sealed record PromptAnswer(string Text, string Model);
