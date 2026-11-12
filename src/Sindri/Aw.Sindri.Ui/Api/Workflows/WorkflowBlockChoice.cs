namespace Aw.Sindri.Ui.Api.Workflows;

/// <summary>One value of a block field's closed list or preset list, as GET /sindri/workflows/blocks serves it.</summary>
/// <param name="Value">What the config stores.</param>
/// <param name="Label">What the picker shows.</param>
internal sealed record WorkflowBlockChoice(string Value, string Label);
