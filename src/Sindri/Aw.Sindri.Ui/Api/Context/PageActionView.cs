namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One action of a <see cref="PageView"/>.</summary>
/// <param name="Id">Its id ('submit-schedule').</param>
/// <param name="Key">The action's node key.</param>
/// <param name="Name">What the map shows.</param>
/// <param name="Mark">'owner' or 'suggested'.</param>
/// <param name="Code">True when a code file implements the action.</param>
/// <param name="Concepts">The concept ids it expresses.</param>
internal sealed record PageActionView(string Id, string Key, string Name, string Mark, bool Code, IReadOnlyList<string> Concepts);
