using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.ConnectAccount;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ConnectAccount;

/// <summary>ConnectAccountService over a recording profile writer, an in-memory connection store and a gh login the test sets.</summary>
internal sealed class ConnectAccountRig
{
    public SliceHarness Slice { get; } = new();

    public RecordingWriter Writer { get; } = new();

    public RecordingInitializer Initializer { get; } = new();

    public InMemoryConnectionStore Connections { get; } = new();

    public FakeGhLogin Gh { get; } = new();

    public FakeDevice Device { get; } = new();

    /// <summary>Gets the live profile the device flow reads `setup.oauth` from; missing until a test sets it.</summary>
    public InMemoryProfiles Profiles { get; } = new();

    public ConnectAccountService Service() =>
        new(Slice.Callers, Gh, new AccountProfileWrites(Writer, new AcceptingParser(), Initializer, new FakeShippedEffects()), Connections, Slice.Clock, DeviceFlow());

    public DeviceAccountSignIn DeviceFlow() =>
        new(Device, new FakeConnectionPresets(GitHubPreset), Profiles, new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

    /// <summary>Gets the shipped github preset's shape, with no shipped client id (first-run spec decision 15a).</summary>
    public static ConnectionPreset GitHubPreset { get; } = new("github", "GitHub", ConnectionKind.OAuth, "https://github.com/login/oauth/authorize", "https://github.com/login/oauth/access_token", RevokeUrl: null,
        DefaultScopes: ["repo", "read:org"], HostGroup: "github", LoopbackAllowed: true, SignInNote: string.Empty, PollableEvents: [], ProbeUrl: "https://api.github.com/user")
    {
        DeviceCodeUrl = "https://github.com/login/device/code",
        ShippedClient = string.Empty,
    };

    /// <summary>Sets the live profile's `setup.oauth.github`.</summary>
    /// <param name="client">The client id.</param>
    public void OwnerApp(string client)
    {
        var profile = new SindriProfile(1, "o") { Setup = new SetupSettings { Oauth = new SetupOauthSettings(Github: client) } };
        Profiles.Live = new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }

    /// <summary>A device flow that hands out one code and finishes when the test says so.</summary>
    internal sealed class FakeDevice : IDeviceSignIn
    {
        public List<string> Clients { get; } = [];

        public TaskCompletionSource<Result<TokenSet>> Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<Result<DeviceAuthorization>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken)
        {
            Clients.Add(client);
            return ValueTask.FromResult<Result<DeviceAuthorization>>(new DeviceAuthorization("dc-secret", "ABCD-1234", new Uri("https://github.com/login/device"), KnownInstants.Epoch.AddMinutes(15), TimeSpan.FromSeconds(5)));
        }

        public async ValueTask<Result<TokenSet>> Complete(ConnectionPreset preset, string client, DeviceAuthorization authorization, CancellationToken cancellationToken) =>
            await Finish.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

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
