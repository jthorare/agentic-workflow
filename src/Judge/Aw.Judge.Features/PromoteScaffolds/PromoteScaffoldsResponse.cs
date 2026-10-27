namespace Aw.Judge.Features.PromoteScaffolds;

/// <summary>The PromoteScaffolds response.</summary>
/// <param name="Verdicts">One verdict per scaffold.</param>
/// <param name="Applied">Whether the go verdicts were written into config.json.</param>
public sealed record PromoteScaffoldsResponse(IReadOnlyList<ScaffoldVerdict> Verdicts, bool Applied);
