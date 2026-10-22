namespace Aw.Sindri.Core.Setup;

/// <summary>The effect kinds the recipes this build ships declare (setup-once spec §5.1): read from the embedded set, so the default profile allows exactly what they emit.</summary>
public interface IShippedEffects
{
    /// <summary>The effect ids ('effect.claim.ref' and so on), sorted, each once.</summary>
    /// <returns>The kinds; empty for a build that embeds no recipes.</returns>
    public IReadOnlyList<string> Kinds();
}
