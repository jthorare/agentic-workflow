using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Launch;

/// <summary>PR review page spec §7.1: a Step launch empties the variable the approved profile's 'review.credential: env:NAME' reads, and the default AW_REVIEW_GITHUB_TOKEN when it is unset; an attended launch is not scrubbed.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LauncherReviewCredentialTests : IDisposable
{
    private readonly string _home = Directory.CreateTempSubdirectory("aw-launcher-review-cred-").FullName;
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);
    private readonly FakeSessionHost _host = new();
    private readonly FakeTerminalViewer _viewer = new();

    public void Dispose() => Directory.Delete(_home, recursive: true);

    private static StepArgs Step() => new(Model: null, Policy: null, new StepRef("scoping", "agent"));

    private Launcher Make(PrReviewSettings? review) =>
        new(new LauncherParts(new AgentCatalog([AgentEntries.ClaudeCode]), _ => _host, _viewer, new LauncherOptions(_home), _clock)
        {
            ResolveBin = static (_, _) => ValueTask.FromResult<string?>(null),
            ReviewCredentialVariables = _ => ValueTask.FromResult<IReadOnlyList<string>>(PrReviewRules.CredentialVariable(review) is { } name ? [name] : []),
        });

    private async Task<IReadOnlyDictionary<string, string>> EnvOf(PrReviewSettings? review, LaunchRequestBuilder request)
    {
        _ = await Make(review).Launch(request.InDirectory(_home).Build(), CancellationToken.None);
        return _host.Started.Single().Env;
    }

    [Fact]
    public async Task A_Step_launch_empties_the_variable_review_credential_names()
    {
        var env = await EnvOf(new PrReviewSettings(Credential: "env:MY_REVIEW_TOKEN"), A.LaunchRequest().Unattended(Step()));

        Assert.Equal(string.Empty, env["MY_REVIEW_TOKEN"]);
        Assert.Equal(string.Empty, env["AW_REVIEW_GITHUB_TOKEN"]);
    }

    [Fact]
    public async Task A_Step_launch_empties_the_default_variable_when_no_credential_is_set()
    {
        var env = await EnvOf(review: null, A.LaunchRequest().Unattended(Step()));

        Assert.Equal(string.Empty, env["AW_REVIEW_GITHUB_TOKEN"]);
    }

    [Fact]
    public async Task A_command_credential_adds_no_variable()
    {
        var env = await EnvOf(new PrReviewSettings(Credential: "command:gh auth token"), A.LaunchRequest().Unattended(Step()));

        Assert.False(env.ContainsKey("MY_REVIEW_TOKEN"));
    }

    [Fact]
    public async Task An_attended_launch_is_not_scrubbed()
    {
        var env = await EnvOf(new PrReviewSettings(Credential: "env:MY_REVIEW_TOKEN"), A.LaunchRequest());

        Assert.False(env.ContainsKey("MY_REVIEW_TOKEN"));
    }
}
