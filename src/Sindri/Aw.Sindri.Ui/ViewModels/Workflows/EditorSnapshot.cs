namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>What the editor header needs to pick its primary button (spec 4.7 and 4.8).</summary>
/// <param name="EngineDown">Serve is not running.</param>
/// <param name="Conflict">A stale-base edit was refused.</param>
/// <param name="Testing">A test run is going.</param>
/// <param name="HasTrigger">The workflow has a trigger.</param>
/// <param name="HasAction">The workflow has a step after the trigger.</param>
/// <param name="Findings">How many findings block.</param>
/// <param name="Tested">The revision has a passing test.</param>
internal sealed record EditorSnapshot(bool EngineDown = false, bool Conflict = false, bool Testing = false, bool HasTrigger = true, bool HasAction = true, int Findings = 0, bool Tested = false);
