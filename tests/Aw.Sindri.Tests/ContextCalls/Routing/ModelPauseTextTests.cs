using Aw.Context.Core.Engine;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The progress line of a job paused for a model harness (model routing spec §5.3 step 1, §8.3), pinned word for word.</summary>
public sealed class ModelPauseTextTests
{
    [Fact, Trait("Lane", "unit")]
    public void One_clause_per_harness_in_its_order_with_its_cause()
    {
        var line = ModelPauseText.Line([(ModelHarness.ClaudeCode, HarnessCause.Limit), (ModelHarness.Codex, HarnessCause.Limit), (ModelHarness.Cursor, HarnessCause.NotInstalled)]);

        Assert.Equal("Paused: claude-code at its usage limit; codex at its usage limit; cursor not installed. Resume when one is back.", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_harness_that_failed_some_other_way_reads_failed()
    {
        var line = ModelPauseText.Line([(ModelHarness.Codex, HarnessCause.Failed), (ModelHarness.ClaudeCode, HarnessCause.Limit)]);

        Assert.Equal("Paused: codex failed; claude-code at its usage limit. Resume when one is back.", line);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_single_harness_has_no_separator()
        => Assert.Equal("Paused: claude-code at its usage limit. Resume when one is back.", ModelPauseText.Line([(ModelHarness.ClaudeCode, HarnessCause.Limit)]));
}
