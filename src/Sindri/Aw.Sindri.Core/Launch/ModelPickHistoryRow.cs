namespace Aw.Sindri.Core.Launch;

/// <summary>How a model fared on one recipe step over its last 20 runs (model-pick spec §7.2).</summary>
/// <param name="Model">The model id.</param>
/// <param name="Runs">The runs counted (at most 20).</param>
/// <param name="Refused">Runs whose output was refused.</param>
/// <param name="CheckFailed">Runs that failed the step's check.</param>
/// <param name="Redraft">Runs that completed but sent the instance back to the draft state.</param>
public sealed record ModelPickHistoryRow(string Model, int Runs, int Refused, int CheckFailed, int Redraft)
{
    /// <summary>True when more than half of the runs were refused or failed their check (model-pick spec §4.5).</summary>
    public bool Unusual => Runs > 0 && (Refused + CheckFailed) * 2 > Runs;
}
