namespace Aw.Sindri.Ui.Api.Home;

/// <summary>One typed fact on a row.</summary>
/// <param name="Kind">'why', 'object' or 'from'.</param>
/// <param name="Text">The fact's text.</param>
internal sealed record HomeFactView(string Kind, string Text);
