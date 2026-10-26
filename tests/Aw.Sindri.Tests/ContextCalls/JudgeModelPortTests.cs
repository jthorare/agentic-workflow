using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Models;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Aw.Sindri.Host.Models;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The context engine's model port over Judge's provider catalog: the owner's agent CLIs only, in the catalog's order.</summary>
public sealed class JudgeModelPortTests
{
    private sealed class Provider(ProviderName name, ProviderResult result) : IModelProvider
    {
        public List<QuestionRef> Asked { get; } = [];

        public ProviderName Name => name;

        public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass> { ContentClass.Code };

        public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
        {
            Asked.Add(question);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Catalog(params Provider[] providers) : IProviderCatalog
    {
        public IReadOnlyCollection<ProviderName>? Allowed { get; private set; }

        public ProviderLineup Lineup(ProvidersConfig? config, IReadOnlyCollection<ProviderName>? allowed, IModelProvider rules)
        {
            Allowed = allowed;
            return new ProviderLineup([.. providers, rules], ChainSpec.Build([AgentCliName.ClaudeCli, AgentCliName.CodexCli], jev: false));
        }

        Result<IModelProvider> IProviderCatalog.Provider(ProviderName name, IReadOnlyCollection<ProviderName>? allowed) =>
            throw new NotSupportedException("the model port asks for a lineup, never one provider by name");

        Result<IModelProvider> IProviderCatalog.Adjudicator(IReadOnlyCollection<ProviderName>? allowed) =>
            throw new NotSupportedException("the model port asks for a lineup, never the adjudicator");
    }

    private static ProviderDecided Answered(string text) =>
        new("answered", 0.9, "ok") { Extra = new JsonObject { ["text"] = text } };

    private static readonly ModelRequest _request = new(ModelPurpose.Labeling, "c1: src/a.ts, src/b.ts", 40, TimeSpan.FromSeconds(30));

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_agent_cli_that_answers_gives_the_text_and_an_estimate_of_the_tokens()
    {
        var claude = new Provider(ProviderName.ClaudeCli, Answered("""[{"id":"c1","label":"Tabs"}]"""));
        var codex = new Provider(ProviderName.CodexCli, Answered("never asked"));
        var port = new JudgeModelPort(() => new Catalog(claude, codex));

        var answer = (await port.Ask(_request, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("""[{"id":"c1","label":"Tabs"}]""", answer.Text);
        Assert.True(answer.InputTokens > 0);
        Assert.True(answer.OutputTokens > 0);
        _ = Assert.Single(claude.Asked);
        Assert.Empty(codex.Asked);
        Assert.Contains(claude.Asked[0].Prompt, "c1: src/a.ts, src/b.ts", StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_request_names_a_model_per_agent_cli_and_each_cli_is_asked_for_its_own()
    {
        var claude = new Provider(ProviderName.ClaudeCli, new ProviderUnavailable(ProviderUnavailable.Timeout));
        var codex = new Provider(ProviderName.CodexCli, Answered("ok"));
        var models = new Dictionary<string, string>(StringComparer.Ordinal) { ["claude"] = "m-claude", ["codex"] = "m-codex" };

        var answer = (await new JudgeModelPort(() => new Catalog(claude, codex)).Ask(_request with { Models = models }, TestContext.Current.CancellationToken)).Value;

        Assert.Equal("m-claude", Assert.Single(claude.Asked).Model);
        Assert.Equal("m-codex", Assert.Single(codex.Asked).Model);
        Assert.Equal("m-codex", answer.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cli_the_request_names_no_model_for_runs_its_own_default()
    {
        var codex = new Provider(ProviderName.CodexCli, Answered("ok"));
        var models = new Dictionary<string, string>(StringComparer.Ordinal) { ["claude"] = "m-claude" };

        var answer = (await new JudgeModelPort(() => new Catalog(codex)).Ask(_request with { Models = models }, TestContext.Current.CancellationToken)).Value;

        Assert.Null(Assert.Single(codex.Asked).Model);
        Assert.Null(answer.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_request_with_no_models_leaves_every_cli_on_its_default()
    {
        var claude = new Provider(ProviderName.ClaudeCli, Answered("ok"));

        var answer = (await new JudgeModelPort(() => new Catalog(claude)).Ask(_request, TestContext.Current.CancellationToken)).Value;

        Assert.Null(Assert.Single(claude.Asked).Model);
        Assert.Null(answer.Model);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_remote_jev_api_is_never_allowed_because_the_prompt_names_files_of_the_owners_repo()
    {
        var catalog = new Catalog(new Provider(ProviderName.ClaudeCli, Answered("x")));

        _ = await new JudgeModelPort(() => catalog).Ask(_request, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ProviderName.Jev, catalog.Allowed!);
        Assert.Contains(ProviderName.ClaudeCli, catalog.Allowed!);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_provider_that_errors_or_times_out_falls_through_to_the_next_and_none_answering_is_042_naming_the_fix()
    {
        var failing = new Provider(ProviderName.ClaudeCli, new ProviderUnavailable(ProviderUnavailable.Timeout));
        var errored = new Provider(ProviderName.CodexCli, new ProviderErrored("bad-json"));
        var port = new JudgeModelPort(() => new Catalog(failing, errored));

        var answer = await port.Ask(_request, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, answer.Error.Code);
        _ = Assert.Single(failing.Asked);
        _ = Assert.Single(errored.Asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_without_text_is_not_an_answer()
    {
        var port = new JudgeModelPort(() => new Catalog(new Provider(ProviderName.ClaudeCli, new ProviderDecided("answered", 0.9, "ok"))));

        var answer = await port.Ask(_request, TestContext.Current.CancellationToken);

        Assert.Equal(ContextErrors.LabelingUnavailable.Code, answer.Error.Code);
    }
}
