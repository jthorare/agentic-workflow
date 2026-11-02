using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.agent' model keys (model-pick spec §8, M-1): shape, SND-MODEL-001 with the nearest id, what a pin matches, and the approval diff.</summary>
public sealed class ModelKeyTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static readonly AgentId[] _agents = [AgentId.From("claude-code"), AgentId.From("codex")];

    private static readonly RecipeStateRef[] _states =
    [
        new("workflow.design", "questions"), new("workflow.scoping", "draft"), new("workflow.scoping", "questions"),
    ];

    private static Dictionary<string, T> Map<T>(params (string Key, T Value)[] pairs) => pairs.ToDictionary(static p => p.Key, static p => p.Value, StringComparer.Ordinal);

    private static WorkflowAgentSettings Agent(
        Dictionary<string, AgentModels>? models = null, Dictionary<string, string>? pin = null, Dictionary<string, string>? cap = null, string? pick = null) =>
        new(Models: models, Pin: pin, Cap: cap, Pick: pick);

    private static SindriProfile With(WorkflowAgentSettings agent) => _valid with { Workflow = new(Agent: agent) };

    private static readonly WorkflowAgentSettings[] _accepted =
    [
        Agent(),
        Agent(pick: "on"),
        Agent(pick: "off"),
        Agent(pin: Map(("claude-code", "haiku"), ("claude-code/questions", "claude-haiku-4-5"), ("codex/draft", "gpt-5.1-codex"))),
        Agent(cap: Map(("claude-code", "sonnet"))),
        Agent(models: Map(("claude-code", new AgentModels(["haiku", "sonnet", "opus"], "sonnet")))),
    ];

    private static readonly (WorkflowAgentSettings Agent, string Key)[] _rejected =
    [
        (Agent(pick: "maybe"), "workflow.agent.pick"),
        (Agent(pick: "ON"), "workflow.agent.pick"),
        (Agent(pin: Map(("Not_An_Id", "haiku"))), "workflow.agent.pin"),
        (Agent(pin: Map(("claude-code/", "haiku"))), "workflow.agent.pin"),
        (Agent(pin: Map(("claude-code/a/b", "haiku"))), "workflow.agent.pin"),
        (Agent(pin: Map(("claude-code", "has space"))), "workflow.agent.pin"),
        (Agent(pin: Map(("claude-code", ""))), "workflow.agent.pin"),
        (Agent(cap: Map(("claude-code/questions", "haiku"))), "workflow.agent.cap"),
        (Agent(cap: Map(("claude-code", "-x"))), "workflow.agent.cap"),
        (Agent(models: Map(("claude-code", new AgentModels([], "sonnet")))), "workflow.agent.models"),
        (Agent(models: Map(("claude-code", new AgentModels(["has space"], "has space")))), "workflow.agent.models"),
        (Agent(models: Map(("Bad", new AgentModels(["haiku"], "haiku")))), "workflow.agent.models"),
    ];

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void The_shape_accepts(int index)
    {
        var agent = _accepted[index];
        Assert.Null(ModelKeyRules.Shape(agent));
        Assert.True(ProfileRules.Check(With(agent)).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void The_shape_rejects_and_names_the_key(int index)
    {
        var (agent, key) = _rejected[index];
        var error = ModelKeyRules.Shape(agent);

        Assert.NotNull(error);
        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains(key, error.Message, StringComparison.Ordinal);
        Assert.True(ProfileRules.Check(With(agent)).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_state_no_recipe_has_names_the_nearest_state()
    {
        var error = ModelKeyRules.Check(Agent(pin: Map(("claude-code/question", "haiku"))), _agents, _states);

        Assert.NotNull(error);
        Assert.Equal("SND-MODEL-001", error.Code);
        Assert.Contains("no state `question` in any recipe; did you mean `questions`?", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_pin_state_far_from_every_state_gets_no_suggestion()
    {
        var error = ModelKeyRules.Check(Agent(pin: Map(("claude-code/zzzzzzzz", "haiku"))), _agents, _states);

        Assert.NotNull(error);
        Assert.Contains("no state `zzzzzzzz` in any recipe", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("did you mean", error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("pin", "claude-cod", "claude-code")]
    [InlineData("cap", "codx", "codex")]
    [InlineData("models", "claude-code2", "claude-code")]
    public void An_agent_id_not_in_the_catalog_names_the_nearest_agent(string section, string typo, string nearest)
    {
        var agent = section switch
        {
            "pin" => Agent(pin: Map((typo, "haiku"))),
            "cap" => Agent(cap: Map((typo, "haiku"))),
            _ => Agent(models: Map((typo, new AgentModels(["haiku"], "haiku")))),
        };

        var error = ModelKeyRules.Check(agent, _agents, _states);

        Assert.NotNull(error);
        Assert.Equal("SND-MODEL-001", error.Code);
        Assert.Contains($"no agent `{typo}` in the launcher catalog; did you mean `{nearest}`?", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_default_outside_the_list_is_refused()
    {
        var error = ModelKeyRules.Check(Agent(models: Map(("claude-code", new AgentModels(["haiku", "sonnet"], "opus")))), _agents, _states);

        Assert.NotNull(error);
        Assert.Equal("SND-MODEL-001", error.Code);
        Assert.Contains("default `opus` is not in list", error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_catalog_or_recipes_only_the_default_is_checked()
    {
        Assert.Null(ModelKeyRules.Check(Agent(pin: Map(("nope/nothing", "haiku"))), agentIds: null, states: null));
        Assert.NotNull(ModelKeyRules.Check(Agent(models: Map(("nope", new AgentModels(["a"], "b")))), agentIds: null, states: null));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_model_id_is_never_checked_against_a_catalog() =>
        Assert.Null(ModelKeyRules.Check(Agent(pin: Map(("claude-code", "model-from-the-future")), cap: Map(("codex", "also-new"))), _agents, _states));

    [Fact, Trait("Lane", "unit")]
    public void A_state_pin_lists_every_recipe_state_it_matches()
    {
        var lines = ModelKeyRules.Matches(Agent(pin: Map(("claude-code/questions", "haiku"))), _states);

        Assert.Equal(["pin claude-code/questions = haiku: matches workflow.design state questions, workflow.scoping state questions"], lines);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude-code", "questions", "haiku-state")]
    [InlineData("claude-code", "draft", "sonnet-agent")]
    [InlineData("codex", "questions", null)]
    public void A_state_pin_beats_the_agent_pin_and_no_pin_is_null(string agentId, string state, string? expected)
    {
        var agent = Agent(pin: Map(("claude-code", "sonnet-agent"), ("claude-code/questions", "haiku-state")));

        Assert.Equal(expected, ModelKeyRules.PinFor(agent, AgentId.From(agentId), state));
        Assert.Null(ModelKeyRules.PinFor(agent: null, agentId: AgentId.From(agentId), state: state));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_agent_pin_and_a_cap_say_what_they_cover_pins_first()
    {
        var lines = ModelKeyRules.Matches(Agent(pin: Map(("codex", "gpt-5")), cap: Map(("claude-code", "sonnet"))), _states);

        Assert.Equal(["pin codex = gpt-5: matches every step of codex", "cap claude-code = sonnet: matches every pick for claude-code"], lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Profile_validate_prints_what_each_pin_matches()
    {
        var checks = new ModelKeyChecks(new FixedAgentCatalog(Entry("claude-code")), new FixedRecipeStateCatalog("workflow.scoping/questions", "workflow.design/questions"));
        var profile = With(Agent(pin: Map(("claude-code/questions", "haiku"))));

        var lines = await checks.PinMatches(profile, TestContext.Current.CancellationToken);

        Assert.Equal(["pin claude-code/questions = haiku: matches workflow.design state questions, workflow.scoping state questions"], lines);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approval_checks_the_keys_against_the_catalog_and_the_recipes()
    {
        var checks = new ModelKeyChecks(new FixedAgentCatalog(Entry("claude-code")), new FixedRecipeStateCatalog("workflow.scoping/questions"));

        var typo = await checks.Models(With(Agent(pin: Map(("claude-code/question", "haiku")))), TestContext.Current.CancellationToken);
        var fine = await checks.Models(With(Agent(pin: Map(("claude-code/questions", "haiku")))), TestContext.Current.CancellationToken);

        Assert.Equal("SND-MODEL-001", typo?.Code);
        Assert.Null(fine);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_keys_are_part_of_the_diff_the_human_approves()
    {
        var changed = ProfileDiff.Between(_valid, With(Agent(
            models: Map(("claude-code", new AgentModels(["haiku", "sonnet"], "sonnet"))),
            pin: Map(("claude-code/questions", "haiku"), ("codex", "gpt-5")),
            cap: Map(("claude-code", "sonnet")),
            pick: "on")));

        Assert.Equal(
            ["workflow.agent.cap.claude-code", "workflow.agent.models.claude-code", "workflow.agent.pick", "workflow.agent.pin.claude-code/questions", "workflow.agent.pin.codex"],   // the diff is sorted by path
            changed);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_keys_keeps_its_hash()
    {
        var plain = System.Text.Encoding.UTF8.GetString(Kernel.Json.AwJson.Serialize(ProfileRules.Check(_valid with { Workflow = new(Agent: new(AgentId.From("claude-code"))) }).Value, SliceHarness.Json));

        Assert.DoesNotContain("models", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("pin", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("pick", plain, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_keys_read_from_the_profile_file()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  agent:\n    target: claude-code\n    pick: on\n    models:\n      claude-code:\n        list: [haiku, sonnet, opus]\n        default: sonnet\n    pin:\n      claude-code/questions: haiku\n    cap:\n      claude-code: sonnet\n");

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        var agent = Assert.IsType<WorkflowAgentSettings>(loaded.Value.Profile.Workflow?.Agent);
        Assert.Equal("on", agent.Pick);
        Assert.Equal(new AgentModels(["haiku", "sonnet", "opus"], "sonnet"), Assert.Single(agent.Models!).Value, new ModelsComparer());
        Assert.Equal("haiku", Assert.Single(agent.Pin!).Value);
        Assert.Equal("sonnet", Assert.Single(agent.Cap!).Value);
    }

    private sealed class ModelsComparer : IEqualityComparer<AgentModels>
    {
        public bool Equals(AgentModels? x, AgentModels? y) => x is not null && y is not null && x.List.SequenceEqual(y.List, StringComparer.Ordinal) && string.Equals(x.Default, y.Default, StringComparison.Ordinal);

        public int GetHashCode(AgentModels obj) => string.GetHashCode(obj.Default, StringComparison.Ordinal);
    }

    private static AgentEntry Entry(string id) => new(AgentId.From(id), id, id, ProcessName: null, Transcripts: null, Resume: null);
}
