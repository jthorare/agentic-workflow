using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>What a skill card is drawn from (skill flows spec §5.2): the block, the state it runs in and what the instance served about it.</summary>
/// <param name="Block">The block behind the state's `step.skill.*` step.</param>
/// <param name="Name">The state in the recipe's words ("Shape the idea").</param>
/// <param name="Cell">The state's cell in the band (its glyph, its loop round).</param>
/// <param name="Run">The state's latest Step run, or null before it starts.</param>
/// <param name="Waiting">Every `launch.owner-waiting` of the instance, oldest first; the card reads the latest of its own state and run.</param>
/// <param name="Facts">The instance's facts; the block's outputs are the ones whose key it names.</param>
/// <param name="Cost">The session's cost in dollars when the launcher reported one, else null.</param>
/// <param name="Launcher">Opens an output.</param>
/// <param name="Clipboard">Where Copy path puts text.</param>
/// <param name="Clock">The clock for the copy confirmation.</param>
internal sealed record SkillCardInput(
    SkillBlockView Block,
    string Name,
    StateCell Cell,
    StepRun? Run,
    IReadOnlyList<SkillWaiting> Waiting,
    IReadOnlyList<FactRow> Facts,
    decimal? Cost,
    ILauncher Launcher,
    IClipboard Clipboard,
    TimeProvider Clock);
