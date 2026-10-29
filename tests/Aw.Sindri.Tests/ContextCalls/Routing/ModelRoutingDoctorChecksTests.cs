using Aw.Context.Core.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Host.Models;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>The doctor's 'context.models' row (context model routing spec §8.4): what will run when ok, and each fix when not.</summary>
[Lane(TestLane.Unit)]
public sealed class ModelRoutingDoctorChecksTests
{
    private static readonly ModelHarness[] _both = [ModelHarness.ClaudeCode, ModelHarness.Codex];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ModelRoutingDoctorChecks Checks(ModelsSettings settings, ModelHarness[] installed, params string[] codexModels) =>
        new(new FixedModels(settings), new FixedCatalog(codexModels), () => installed);

    private static ModelsSettings Settings(ModelEntry? @default = null, Dictionary<ModelKind, ModelEntry>? kinds = null) =>
        new() { Default = @default ?? new ModelEntry(Models: new Dictionary<ModelHarness, string> { [ModelHarness.ClaudeCode] = "haiku", [ModelHarness.Codex] = "gpt-6-luna" }), Kinds = kinds ?? [] };

    private static async Task<DoctorCheck> Row(ModelRoutingDoctorChecks checks) => Assert.Single(await checks.Run(Token));

    [Fact]
    public async Task Kinds_that_run_the_same_route_share_one_line_and_the_row_is_ok()
    {
        var kinds = new Dictionary<ModelKind, ModelEntry>
        {
            [ModelKind.ConceptNaming] = new(Models: new Dictionary<ModelHarness, string> { [ModelHarness.ClaudeCode] = "sonnet", [ModelHarness.Codex] = "gpt-5.5" }),
        };

        var row = await Row(Checks(Settings(kinds: kinds), _both, "gpt-6-luna", "gpt-5.5"));

        Expect.Equal("context:models", row.Name);
        Expect.Equal(CheckState.Ok, row.State);
        Expect.Equal("labeling, decisionExtraction, adjudication, mapCleanup, rerank: claude-code haiku, then codex gpt-6-luna; conceptNaming: claude-code sonnet, then codex gpt-5.5", row.Detail);
        Assert.Null(row.Fix);
    }

    [Fact]
    public async Task A_harness_the_profile_lists_but_that_is_not_installed_warns_with_the_key_to_edit()
    {
        var entry = new ModelEntry(Harnesses: [ModelHarness.ClaudeCode, ModelHarness.Codex, ModelHarness.Cursor]);

        var row = await Row(Checks(Settings(entry), _both));

        Expect.Equal(CheckState.Warn, row.State);
        Assert.Contains("cursor is not installed", row.Detail, StringComparison.Ordinal);
        Assert.Contains("remove it from context.models.default.harnesses", row.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_list_of_one_harness_warns_that_there_is_no_fallback()
    {
        var kinds = new Dictionary<ModelKind, ModelEntry> { [ModelKind.Labeling] = new(Harnesses: [ModelHarness.Codex]) };

        var row = await Row(Checks(Settings(kinds: kinds), _both));

        Expect.Equal(CheckState.Warn, row.State);
        Assert.Contains("labeling lists one harness, so it has no fallback", row.Detail, StringComparison.Ordinal);
        Assert.Contains("add a second harness to context.models.labeling.harnesses", row.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_codex_model_the_codex_list_does_not_hold_warns()
    {
        var entry = new ModelEntry(Models: new Dictionary<ModelHarness, string> { [ModelHarness.ClaudeCode] = "haiku", [ModelHarness.Codex] = "gpt-9" });

        var row = await Row(Checks(Settings(entry), _both, "gpt-6-luna"));

        Expect.Equal(CheckState.Warn, row.State);
        Assert.Contains("codex model gpt-9 is not in codex's model list", row.Detail, StringComparison.Ordinal);
        Assert.Contains("codex debug models", row.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_codex_list_that_could_not_be_read_checks_nothing_and_Claude_is_never_checked()
    {
        var entry = new ModelEntry(Models: new Dictionary<ModelHarness, string> { [ModelHarness.ClaudeCode] = "whatever", [ModelHarness.Codex] = "gpt-9" });

        var row = await Row(Checks(Settings(entry), _both));

        Expect.Equal(CheckState.Ok, row.State);
    }

    [Fact]
    public async Task With_no_approved_profile_the_row_is_ok_and_says_not_configured_without_reading_PATH()
    {
        var checks = new ModelRoutingDoctorChecks(new FixedModels(new ModelsSettings(), approved: false), new FixedCatalog([]), () => throw new InvalidOperationException("PATH was read"));

        var row = await Row(checks);

        Expect.Equal((CheckState.Ok, "not configured (no profile is approved)"), (row.State, row.Detail));
        Assert.Null(row.Fix);
    }

    [Fact]
    public async Task No_installed_harness_warns_that_no_model_call_can_run()
    {
        var row = await Row(Checks(Settings(), []));

        Expect.Equal(CheckState.Warn, row.State);
        Assert.Contains("no agent CLI", row.Detail, StringComparison.Ordinal);
    }

    private sealed class FixedCatalog(string[] codex) : IModelCatalog
    {
        public ValueTask<IReadOnlyList<ModelFacts>> ModelsOf(AgentId agent, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<ModelFacts>>(string.Equals(agent.Value, "codex", StringComparison.Ordinal) ? [.. codex.Select(static slug => new ModelFacts(slug, slug, Aw.Sindri.Core.Launch.ModelSource.Discovered))] : []);
    }
}
