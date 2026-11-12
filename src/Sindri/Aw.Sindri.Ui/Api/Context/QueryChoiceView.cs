namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One suggestion of a question's answer: the key a pick reads, the label a person reads, the node's kind.</summary>
/// <param name="Key">The node key.</param>
/// <param name="Label">The node's label, or null.</param>
/// <param name="Kind">The node's kind, or null.</param>
internal sealed record QueryChoiceView(string Key, string? Label, string? Kind);
