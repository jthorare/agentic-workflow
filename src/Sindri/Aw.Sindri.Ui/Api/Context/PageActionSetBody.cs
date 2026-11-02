namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One action of a <see cref="PageSetBody"/>.</summary>
/// <param name="Id">The action id; null derives it from the name.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Action">The map action it names; null for an owner action.</param>
/// <param name="Concepts">The concept ids it relates to.</param>
internal sealed record PageActionSetBody(string? Id, string Name, string? Action, IReadOnlyList<string> Concepts);
