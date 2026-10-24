using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Step;
using Aw.Sindri.Features.Pr;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'step.docs.update' reads the docs worktree and is told to run 'sindri docs check' on its own facts before it reports (docs-sync spec §7.2, API17).</summary>
[Lane(TestLane.Unit)]
public sealed class DocsUpdateInstructionsTests
{
    [Fact]
    public void The_Step_declares_the_workspace_and_the_docs_scope_as_input_facts()
    {
        Assert.Contains(FactKey.From("fact:workspace"), DocsSteps.Update.Inputs);
        Assert.Contains(FactKey.From("fact:docsScope"), DocsSteps.Update.Inputs);
    }

    [Fact]
    public void The_brief_names_the_check_command_and_tells_the_agent_to_fix_findings()
    {
        var all = string.Join('\n', new DocsUpdateInstructions().Lines);

        Assert.Contains("sindri docs check --facts \"$AW_SINDRI_RUN_DIR\"", all, StringComparison.Ordinal);
        Assert.Contains("fix every finding", all, StringComparison.Ordinal);
    }

    [Fact]
    public void The_instructions_are_for_the_docs_update_Step_only()
    {
        Assert.True(new DocsUpdateInstructions().IsFor(BlockId.From("step.docs.update")));
        Assert.False(new DocsUpdateInstructions().IsFor(BlockId.From("step.docs.check")));
    }
}
