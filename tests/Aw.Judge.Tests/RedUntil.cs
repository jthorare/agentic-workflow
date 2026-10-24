namespace Aw.Judge.Tests;

/// <summary>
/// The skip reason of a red test the cutover contract unit (6.0) wrote ahead of the unit that makes it pass (spec §12 P0).
/// The unit that lands the behavior removes the Skip from its tests and completes their set-up (the stub takes no ports
/// yet, so a test builds its fakes beside the stub and the unit passes them in).
/// </summary>
internal static class RedUntil
{
    /// <summary>Unit 6.1: the prompt sorter, its scaffold switch and the promptSort config write.</summary>
    public const string Sorter = "red until cutover unit 6.1";

    /// <summary>Unit 6.2: trait-check.</summary>
    public const string TraitCheck = "red until cutover unit 6.2";
}
