namespace Aw.Sindri.Features.ListWorkflowBlocks;

/// <summary>One pickable value of a field.</summary>
/// <param name="Value">What the config stores.</param>
/// <param name="Label">What the picker shows.</param>
internal sealed record BlockChoiceView(string Value, string Label);
