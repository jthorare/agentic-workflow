namespace Aw.Sindri.Core.Prove;

/// <summary>A phase's hidden set as the store holds it (<c language="text">set.json</c>).</summary>
/// <param name="Phase">The phase key.</param>
/// <param name="Scenarios">Its scenarios, the canary among them.</param>
/// <param name="Hash">'sha256:' and the hex digest of the set file's bytes, which the seal must name.</param>
public sealed record HoldoutSet(string Phase, IReadOnlyList<HoldoutScenario> Scenarios, string Hash)
{
    /// <summary>The canary token phrase; null when the set has no canary scenario.</summary>
    public string? Canary => Scenarios.FirstOrDefault(static s => s.IsCanary)?.Phrase;
}
