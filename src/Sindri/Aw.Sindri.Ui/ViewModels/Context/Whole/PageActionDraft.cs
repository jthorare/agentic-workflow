namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One action of a <see cref="PageDraft"/>.</summary>
/// <param name="Id">The action id, or null for a new one (the server makes it from the name).</param>
/// <param name="Name">The name.</param>
/// <param name="Action">The map action's node key it names, or null.</param>
/// <param name="Concepts">The concept ids it relates to, in order.</param>
internal sealed record PageActionDraft(string? Id, string Name, string? Action, IReadOnlyList<string> Concepts);
