using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Pr;

/// <summary>What 'step.docs.update' is told (docs-sync spec §7.2): check its own docs commit with 'sindri docs check' before it reports.</summary>
internal sealed class DocsUpdateInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.docs.update");

    private static readonly string[] _lines =
    [
        "Goal: the listed docs this pull request makes wrong or incomplete, corrected, and nothing else. Work in the docs worktree you were launched in (fact:workspace) and commit once there.",
        "Before you report, run: sindri docs check --facts \"$AW_SINDRI_RUN_DIR\" --json. It reads workspace.json and docsScope.json from the run folder and checks your commit for doc paths only, links that resolve, closed code fences and a real change.",
        "Read its findings and fix every finding, commit the fix, and run the check again. Report changed only when the check passes; if a finding can't be fixed, report blocked and list it with why in fact:handoffNote.",
    ];

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => _lines;
}
