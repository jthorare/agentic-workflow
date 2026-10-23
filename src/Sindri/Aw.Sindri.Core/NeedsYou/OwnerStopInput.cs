namespace Aw.Sindri.Core.NeedsYou;

/// <summary>What the 'desk:' row of an owner stop says (<see cref="NeedsYouRows.ForOwnerStop"/>).</summary>
/// <param name="Label">The row's short label ("ready to merge").</param>
/// <param name="Reason">The sentence that tells the owner what waits on them.</param>
/// <param name="Pr">The pull request the instance is about, when its status fact names one.</param>
public sealed record OwnerStopInput(string Label, string Reason, NeedsYouOpenPr? Pr);
