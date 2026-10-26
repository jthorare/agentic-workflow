using System.Globalization;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Engine;
using Xunit;

namespace Aw.Sindri.Testing.Scoping;

/// <summary>
/// The contract every <see cref="IScopeRunRecords"/> honours (issue 966, spec 6): one record per agent run of the instance, oldest first, with the model the session
/// used (its latest usage sample), the one the state asked for, and a completion at the desk as a record of its own.
/// </summary>
public abstract class ScopeRunRecordsContract
{
    private static readonly InstanceAddress _scoping = InstanceAddress.From("T-1/scoping/1");
    private static readonly InstanceAddress _other = InstanceAddress.From("T-2/scoping/1");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The records under test, over a recipe whose states are 'draft' (asks for 'claude-sonnet-5-5'), 'gap-search' (asks for 'claude-opus-5-5') and 'challenge' (asks for none).</summary>
    /// <returns>The records.</returns>
    protected abstract ValueTask<IScopeRunRecords> Create();

    /// <summary>Records the start of <paramref name="launch"/>.</summary>
    /// <param name="records">The records from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Launched(IScopeRunRecords records, ScopeLaunch launch);

    /// <summary>Records usage sample <paramref name="sample"/> of <paramref name="launch"/>.</summary>
    /// <param name="records">The records from <see cref="Create"/>.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="sample">The sample's number, from 1.</param>
    /// <param name="tokens">Input and output tokens so far.</param>
    /// <param name="model">The model the tokens are priced as; null when the reader named none.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask Spent(IScopeRunRecords records, LaunchId launch, int sample, long tokens, string? model);

    /// <summary>Records a completion of a state's Step at the desk: a run with no launch.</summary>
    /// <param name="records">The records from <see cref="Create"/>.</param>
    /// <param name="run">The instance.</param>
    /// <param name="state">The state.</param>
    /// <param name="number">The run's number.</param>
    /// <returns>Done.</returns>
    protected abstract ValueTask CompletedAtTheDesk(IScopeRunRecords records, InstanceAddress run, string state, int number);

    private static ScopeLaunch Draft(int number = 1) => new(_scoping, "draft", "step.scope.draft", "claude-sonnet-5-5", number, LaunchId.From("LCH-0000000000000000000000000000000" + number.ToString(CultureInfo.InvariantCulture)));

    private static ScopeLaunch GapSearch(int number = 1) => new(_scoping, "gap-search", "step.scope.gap-search", "claude-opus-5-5", number, LaunchId.From("LCH-0000000000000000000000000000001" + number.ToString(CultureInfo.InvariantCulture)));

    /// <summary>An instance that has run nothing has no records.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_instance_with_no_runs_has_no_records()
    {
        var records = await Create();

        var read = await records.Of(_scoping, Token);

        Expect.True(read.IsSuccess && read.Value.Count == 0);
    }

    /// <summary>A launch with usage is an agent record: the step, state, round, the model used and asked for, its vendor and tokens.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_launch_with_usage_is_an_agent_record_with_the_model_it_used()
    {
        var records = await Create();
        var launch = GapSearch(2);
        await Launched(records, launch);
        await Spent(records, launch.Launch, 1, 1_000, "claude-opus-5-5");

        var read = await records.Of(_scoping, Token);

        var record = Expect.Only(read.Value);
        Expect.Equal(new ScopeRunRecord("step.scope.gap-search", "gap-search", 2, "agent", "claude-opus-5-5", "claude-opus-5-5", "anthropic", 1_000, launch.Launch.Value), record);
    }

    /// <summary>A launch no usage was sampled for has no model and no tokens, and still shows what it asked for.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_launch_with_no_usage_has_no_model_and_no_tokens()
    {
        var records = await Create();
        var launch = Draft();
        await Launched(records, launch);

        var read = await records.Of(_scoping, Token);

        var record = Expect.Only(read.Value);
        Expect.Equal(new ScopeRunRecord("step.scope.draft", "draft", 1, "agent", Model: null, "claude-sonnet-5-5", Vendor: null, 0, launch.Launch.Value), record);
    }

    /// <summary>The latest sample is the run's model and tokens (samples are cumulative).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_latest_usage_sample_is_the_model_and_the_tokens()
    {
        var records = await Create();
        var launch = Draft();
        await Launched(records, launch);
        await Spent(records, launch.Launch, 1, 500, "claude-haiku-5-5");
        await Spent(records, launch.Launch, 2, 1_500, "claude-sonnet-5-5");

        var read = await records.Of(_scoping, Token);

        var record = Expect.Only(read.Value);
        Expect.Equal("claude-sonnet-5-5", record.Model);
        Expect.Equal(1_500L, record.Tokens);
    }

    /// <summary>A state that asks for no model has no requested model.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_state_that_asks_for_no_model_has_no_requested_model()
    {
        var records = await Create();
        var launch = new ScopeLaunch(_scoping, "challenge", "step.scope.challenge", Requested: null, 1, LaunchId.From("LCH-00000000000000000000000000000021"));
        await Launched(records, launch);
        await Spent(records, launch.Launch, 1, 100, "gpt-5");

        var read = await records.Of(_scoping, Token);

        var record = Expect.Only(read.Value);
        Expect.Equal(expected: null, record.Requested);
        Expect.Equal("openai", record.Vendor);
    }

    /// <summary>A completion at the desk is a record with no launch and no model: the model is not known.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_completion_at_the_desk_is_a_desk_record_with_no_launch()
    {
        var records = await Create();
        await CompletedAtTheDesk(records, _scoping, "draft", 1);

        var read = await records.Of(_scoping, Token);

        var record = Expect.Only(read.Value);
        Expect.Equal(new ScopeRunRecord("step.scope.draft", "draft", 1, "desk", Model: null, "claude-sonnet-5-5", Vendor: null, 0, Launch: null), record);
    }

    /// <summary>Runs come oldest first, and a run launched is not also counted as completed at the desk.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Records_are_oldest_first()
    {
        var records = await Create();
        await Launched(records, Draft());
        await CompletedAtTheDesk(records, _scoping, "challenge", 1);
        await Launched(records, GapSearch());

        var read = await records.Of(_scoping, Token);

        Expect.Sequence(["draft", "challenge", "gap-search"], read.Value.Select(static r => r.State));
        Expect.Sequence(["agent", "desk", "agent"], read.Value.Select(static r => r.Via));
    }

    /// <summary>Another instance's runs are not this one's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Another_instances_runs_are_not_included()
    {
        var records = await Create();
        await Launched(records, Draft() with { Run = _other });
        await Launched(records, GapSearch());

        var read = await records.Of(_scoping, Token);

        Expect.Sequence(["gap-search"], read.Value.Select(static r => r.State));
    }
}
