using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>The box readers (model-pick spec §3): what each agent's list is read from.</summary>
[Lane(TestLane.Unit)]
public sealed class ProbedModelCatalogTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ProbedModelCatalog Catalog(ProbedModelCatalog.Run run) => new(ProbedModelCatalog.Readers(run));

    private static ValueTask<string?> Never(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"nothing should run: {program}");

    [Fact]
    public async Task Claude_code_lists_its_catalog_without_running_anything()
    {
        var models = await Catalog(Never).ModelsOf(AgentId.From("claude-code"), Token);

        Expect.Equal("haiku,sonnet,opus", string.Join(',', models.Select(static m => m.Slug)));
    }

    [Fact]
    public async Task Codex_lists_what_codex_debug_models_prints()
    {
        var calls = new List<string>();
        var catalog = Catalog((program, arguments, _) =>
        {
            calls.Add(program + " " + string.Join(' ', arguments));
            return ValueTask.FromResult<string?>("""{"models":[{"slug":"gpt-5.4","visibility":"list","priority":2},{"slug":"gpt-5.2","visibility":"list","priority":10}]}""");
        });

        var models = await catalog.ModelsOf(AgentId.From("codex"), Token);

        Expect.Equal("codex debug models", string.Join(';', calls));
        Expect.Equal("gpt-5.4,gpt-5.2", string.Join(',', models.Select(static m => m.Slug)));
    }

    [Fact]
    public async Task Codex_with_a_failed_command_offers_no_models()
    {
        var models = await Catalog(static (_, _, _) => ValueTask.FromResult<string?>(null)).ModelsOf(AgentId.From("codex"), Token);

        Expect.Equal(0, models.Count);
    }

    [Fact]
    public async Task An_agent_with_no_reader_offers_no_models_and_nothing_runs()
    {
        var models = await Catalog(Never).ModelsOf(AgentId.From("cursor"), Token);

        Expect.Equal(0, models.Count);
    }
}
