namespace Aw.Sindri.Core.Launch;

/// <summary>The Step an unattended launch runs, as the seed prompt names it (engine spec §6.5).</summary>
/// <param name="State">The recipe state the Step belongs to.</param>
/// <param name="Block">The block in that state whose descriptor supplies the seed prompt.</param>
public sealed record StepRef(string State, string Block);
