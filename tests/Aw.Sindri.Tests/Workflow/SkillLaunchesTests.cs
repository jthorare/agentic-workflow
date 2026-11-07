using System.Text;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Infrastructure.Skills;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Skills;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// What 'SkillLaunches.Of' makes of a Step that runs a skill block (skill flows spec §3.3): whose invocation form the seed takes from the agent's id, a stored fact read back as the text it holds,
/// and a block that isn't loaded. The blocks are read from the fixture folder, so the integration lane.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class SkillLaunchesTests
{
    private static readonly LoadedSkillBlock _review = SkillManifestLoader.Load(SkillFixtures.Folder).Loaded.Single(static b => string.Equals(b.Manifest.Name, "review", StringComparison.Ordinal));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AgentLaunchRun Run(IReadOnlyDictionary<string, WithValue>? with = null, IReadOnlyDictionary<FactKey, FactValue>? facts = null)
    {
        var address = InstanceAddress.From("T-1/review/1");
        var run = new AgentStepRun(WorkflowInstanceId.Of(address), address, RunToken.From("1.1"), new StepRef("run", _review.Descriptor.Id.Value), WriteScope: false);
        return new AgentLaunchRun(run, Item: null, Facts: facts, SkillWith: with ?? new Dictionary<string, WithValue>(StringComparer.Ordinal));
    }

    private static AgentLaunchInputs Inputs(string stored = "", params LoadedSkillBlock[] loaded) =>
        new(Bundles: null!, new StoredText(stored), Instructions: [], new PassThroughScrubber(), [new Blocks(loaded)], TimeProvider.System);   // why: a skill launch reads neither the bundles nor the instructions

    private static async Task<SkillLaunch> Launched(string agent)
    {
        var made = await SkillLaunches.Of(Run(), _review.Descriptor.Id, AgentId.From(agent), Inputs(loaded: _review), Token);
        return Assert.IsType<SkillLaunch>(made.Value);
    }

    [Theory]
    [InlineData("codex", SkillTarget.Codex)]
    [InlineData("codex-cli", SkillTarget.Codex)]
    [InlineData("claude-code", SkillTarget.ClaudeCode)]
    [InlineData("my-codex", SkillTarget.ClaudeCode)]   // why: only an id that starts with 'codex' is Codex's
    public async Task The_agents_id_picks_the_invocation_form_the_seed_takes(string agent, SkillTarget expected)
    {
        var launch = await Launched(agent);

        Assert.Equal(expected, launch.Target);
    }

    [Fact]
    public async Task A_block_that_is_not_loaded_is_an_invalid_request_that_says_so()
    {
        var other = BlockId.From(_review.Descriptor.Id.Value + "x");

        var made = await SkillLaunches.Of(Run(), other, AgentId.From("claude-code"), Inputs(loaded: _review), Token);

        Assert.True(made.IsError);
        Assert.Equal(SindriErrors.RequestInvalid.Code, made.Error.Code);
        Assert.Contains("is not loaded", made.Error.Message, StringComparison.Ordinal);
        Assert.Contains(other.Value, made.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_stored_fact_that_holds_a_json_string_is_the_input_without_its_quotes()
    {
        var launch = await FromFact("\"fix the login\"");

        Assert.Equal("fix the login", launch.Inputs["pr"]);
    }

    [Theory]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2]")]
    [InlineData("42")]
    [InlineData("not json at all")]
    public async Task A_stored_fact_that_holds_anything_else_is_the_input_as_stored(string stored)
    {
        var launch = await FromFact(stored);

        Assert.Equal(stored, launch.Inputs["pr"]);
    }

    [Fact]
    public void Unwrapped_takes_a_json_string_out_of_its_quotes_and_leaves_other_text_alone()
    {
        Assert.Equal("hello", SkillLaunches.Unwrapped("\"hello\""));
        Assert.Equal("a\"b", SkillLaunches.Unwrapped("\"a\\\"b\""));
        Assert.Equal("{\"a\":1}", SkillLaunches.Unwrapped("{\"a\":1}"));
        Assert.Equal("plain", SkillLaunches.Unwrapped("plain"));
    }

    // The review block's 'pr' input given as a fact, which holds <stored>.
    private static async Task<SkillLaunch> FromFact(string stored)
    {
        var key = FactKey.From("fact:pr");
        var facts = new Dictionary<FactKey, FactValue> { [key] = new FactValue("fact:sha256:" + new string('a', 64), new string('a', 64), HeadSha: null) };
        var with = new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["pr"] = new(Literal: null, key, Profile: null) };

        var made = await SkillLaunches.Of(Run(with, facts), _review.Descriptor.Id, AgentId.From("claude-code"), Inputs(stored, _review), Token);

        return Assert.IsType<SkillLaunch>(made.Value);
    }

    private sealed class StoredText(string text) : IFactContents
    {
        public ValueTask<Result<ReadOnlyMemory<byte>>> Read(FactValue fact, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Success((ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(text)));
    }

    private sealed class Blocks(IReadOnlyList<LoadedSkillBlock> loaded) : ISkillBlocks
    {
        public string Folder => SkillFixtures.Folder;

        public IReadOnlyList<LoadedSkillBlock> Loaded => loaded;

        public IReadOnlyList<SkippedSkillBlock> Skipped => [];

        public SkillBlocks LoadFolder(string folder) => new([], []);
    }
}
