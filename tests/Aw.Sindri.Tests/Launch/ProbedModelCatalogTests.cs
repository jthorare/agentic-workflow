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

    private static ProbedModelCatalog Catalog(ProbedModelCatalog.Run run, bool cursorInstalled = true) => new(ProbedModelCatalog.Readers(run, name => cursorInstalled || name != "cursor-agent"));

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
    public async Task Cursor_lists_what_cursor_agent_list_models_prints()
    {
        var calls = new List<string>();
        var catalog = Catalog((program, arguments, _) =>
        {
            calls.Add(program + " " + string.Join(' ', arguments));
            return ValueTask.FromResult<string?>("Available models\n\nauto - Auto\ngpt-5 - GPT-5 (current)\nsonnet-4.5 - Claude 4.5 Sonnet (default)\n\nTip: use --model <id>\n");
        });

        var models = await catalog.ModelsOf(AgentId.From("cursor"), Token);

        Expect.Equal("cursor-agent --list-models", string.Join(';', calls));
        Expect.Equal("auto,gpt-5,sonnet-4.5", string.Join(',', models.Select(static m => m.Slug)));
        Expect.Equal("GPT-5", models[1].DisplayName);
        Expect.True(models.All(static m => m.Source == ModelSource.Discovered));
    }

    [Fact]
    public async Task Cursor_strips_colour_codes_from_its_list()
    {
        var catalog = Catalog(static (_, _, _) => ValueTask.FromResult<string?>("\u001b[1mAvailable models\u001b[0m\n\u001b[32mgpt-5\u001b[0m - GPT-5\n"));

        var models = await catalog.ModelsOf(AgentId.From("cursor"), Token);

        Expect.Equal("gpt-5", string.Join(',', models.Select(static m => m.Slug)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not logged in. Run `cursor-agent login`.")]
    public async Task Cursor_without_a_live_list_falls_back_to_the_documented_one(string? output)
    {
        var models = await Catalog((_, _, _) => ValueTask.FromResult(output)).ModelsOf(AgentId.From("cursor"), Token);

        Expect.True(models.Count > 0);
        Expect.True(models.All(static m => m.Source == ModelSource.Catalog));
        Expect.True(models.Any(static m => m.Slug == "gpt-5"));
        Expect.True(models.Any(static m => m.Slug == "auto"));
    }

    [Fact]
    public async Task Cursor_that_is_not_installed_offers_no_models_and_nothing_runs()
    {
        var models = await Catalog(Never, cursorInstalled: false).ModelsOf(AgentId.From("cursor"), Token);

        Expect.Equal(0, models.Count);
    }

    [Fact]
    public async Task An_agent_with_no_reader_offers_no_models_and_nothing_runs()
    {
        var models = await Catalog(Never).ModelsOf(AgentId.From("some-other-agent"), Token);

        Expect.Equal(0, models.Count);
    }
}
