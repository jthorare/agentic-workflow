using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>ConnectAccountService over a recording profile writer, an in-memory connection store and a gh login the test sets.</summary>
internal sealed class ConnectAccountRig
{
    public SliceHarness Slice { get; } = new();

    public RecordingWriter Writer { get; } = new();

    public RecordingInitializer Initializer { get; } = new();

    public InMemoryConnectionStore Connections { get; } = new();

    public FakeGhLogin Gh { get; } = new();

    public ConnectAccountService Service() =>
        new(Slice.Callers, Gh, new AccountProfileWrites(Writer, new AcceptingParser(), Initializer, new FakeShippedEffects()), Connections, Slice.Clock);

    internal sealed class FakeGhLogin : IGitHubCliLogin
    {
        public string? SignedInAs { get; set; }

        public ValueTask<string?> Login(CancellationToken cancellationToken) => ValueTask.FromResult(SignedInAs);
    }

    internal sealed class RecordingWriter : IProfileKeyWriter
    {
        public List<string> Calls { get; } = [];

        public RecordingInitializer? MissingUntilInitialized { get; set; }

        public Result<ProfileKeyEdit> Plan(string keyPath, string value)
        {
            Calls.Add($"plan {keyPath}={value}");
            return Planned();
        }

        public Result<ProfileKeyEdit> PlanText(string keyPath, string value)
        {
            Calls.Add($"text {keyPath}={value}");
            return Planned();
        }

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
            MissingUntilInitialized is { Created: 0 } ? SindriErrors.ProfileMissing.With("no profile") : new ProfileKeyEdit("/home/profile.yaml", "text", Changed: true);
    }

    internal sealed class RecordingInitializer : IProfileInitializer
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
