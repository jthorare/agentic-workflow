namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One node a connect may go to.</summary>
/// <param name="Id">The node's id.</param>
/// <param name="Text">What the list shows.</param>
internal sealed record ConnectTarget(string Id, string Text);
