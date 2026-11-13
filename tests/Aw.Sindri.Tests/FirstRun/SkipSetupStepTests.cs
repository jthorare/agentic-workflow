using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.SkipSetupStep;

namespace Aw.Sindri.Tests.FirstRun;

/// <summary>SkipSetupStep (first-run spec §3.1): only the accounts step (`sources`) and the hello-sindri step (`first-workflow`) can be skipped; a skip sets a scalar key of the live profile and an undo removes it.</summary>
public sealed class SkipSetupStepTests
{
    private readonly RecordingWriter _writer = new();
    private readonly RecordingInitializer _initializer = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private SkipSetupStepService Service() => new(_writer, new AcceptingParser(), _initializer, new NoShippedEffects());

    [Theory, Trait("Lane", "unit")]
    [InlineData(SetupStepId.Sources, "setup.skipAccounts")]
    [InlineData(SetupStepId.FirstWorkflow, "setup.skipFirstWorkflow")]
    public async Task A_skip_sets_the_steps_key_to_true_and_saves_it(SetupStepId step, string key)
    {
        var result = await Service().Handle(new SkipSetupStepRequest(step, Skipped: true), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(new SkipSetupStepResponse(step, Skipped: true, Changed: true), result.Value);
        Assert.Equal([$"plan {key}=true", "save"], _writer.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(SetupStepId.Sources, "setup.skipAccounts")]
    [InlineData(SetupStepId.FirstWorkflow, "setup.skipFirstWorkflow")]
    public async Task An_undo_removes_the_steps_key(SetupStepId step, string key)
    {
        var result = await Service().Handle(new SkipSetupStepRequest(step, Skipped: false), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(new SkipSetupStepResponse(step, Skipped: false, Changed: true), result.Value);
        Assert.Equal([$"remove {key}", "save"], _writer.Calls);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(SetupStepId.Repo)]
    [InlineData(SetupStepId.Serve)]
    [InlineData(SetupStepId.Mcp)]
    public async Task A_step_that_may_not_be_skipped_is_refused_as_invalid_input_and_writes_nothing(SetupStepId step)
    {
        var result = await Service().Handle(new SkipSetupStepRequest(step, Skipped: true), Token);

        Assert.True(result.IsError);
        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_writer.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_skip_on_a_box_with_no_profile_creates_the_profile_then_sets_the_key()
    {
        _writer.MissingUntilInitialized = _initializer;

        var result = await Service().Handle(new SkipSetupStepRequest(SetupStepId.Sources, Skipped: true), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(1, _initializer.Created);
        Assert.Equal(["plan setup.skipAccounts=true", "plan setup.skipAccounts=true", "save"], _writer.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_undo_on_a_box_with_no_profile_has_nothing_to_remove_and_creates_nothing()
    {
        _writer.MissingUntilInitialized = _initializer;

        var result = await Service().Handle(new SkipSetupStepRequest(SetupStepId.Sources, Skipped: false), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal((false, 0), (result.Value.Changed, _initializer.Created));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_skip_the_profile_already_holds_is_unchanged_and_writes_no_file()
    {
        _writer.Changed = false;

        var result = await Service().Handle(new SkipSetupStepRequest(SetupStepId.Sources, Skipped: true), Token);

        Assert.False(result.Value.Changed);
        Assert.Equal(["plan setup.skipAccounts=true", "save"], _writer.Calls);   // Save itself writes nothing for an unchanged plan
    }

    private sealed class RecordingWriter : IProfileKeyWriter
    {
        public List<string> Calls { get; } = [];

        public bool Changed { get; set; } = true;

        public RecordingInitializer? MissingUntilInitialized { get; set; }

        public Result<ProfileKeyEdit> Plan(string keyPath, string value)
        {
            Calls.Add($"plan {keyPath}={value}");
            return Planned();
        }

        public Result<ProfileKeyEdit> PlanText(string keyPath, string value) => throw new NotSupportedException();

        public Result<ProfileKeyEdit> PlanTexts(IReadOnlyList<(string KeyPath, string Value)> values) => throw new NotSupportedException();

        public Result<ProfileKeyEdit> PlanRemove(string keyPath)
        {
            Calls.Add($"remove {keyPath}");
            return Planned();
        }

        public Result<string> Save(ProfileKeyEdit edit)
        {
            Calls.Add("save");
            return edit.Path;
        }

        private Result<ProfileKeyEdit> Planned() =>
            MissingUntilInitialized is { Created: 0 } ? SindriErrors.ProfileMissing.With("no profile") : new ProfileKeyEdit("/home/profile.yaml", "text", Changed);
    }

    private sealed class RecordingInitializer : IProfileInitializer
    {
        public int Created { get; private set; }

        public ValueTask<Result<string>> Create(IReadOnlyCollection<string> defaultEffects, CancellationToken cancellationToken)
        {
            Created++;
            return ValueTask.FromResult<Result<string>>("/home/profile.yaml");
        }
    }

    private sealed class AcceptingParser : IProfileTextParser
    {
        public Result<LoadedProfile> Parse(string yaml, string source)
        {
            var profile = new SindriProfile(1, "o");
            return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), source);
        }

        public Result<LoadedProfile> Recheck(SindriProfile profile, string source) => throw new NotSupportedException();
    }
}
