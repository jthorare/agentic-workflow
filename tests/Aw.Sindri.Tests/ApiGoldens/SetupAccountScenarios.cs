using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of `POST` and `DELETE /setup/accounts/github` (first-run spec §4.2, the accounts step): the device code answer on a box with no gh login, the token-needed answer when no client id is
/// set, and the disconnect. gh and GitHub's device endpoint are fakes, because the real ones read this machine and github.com; the real adapters are tested apart (GhCliLoginTests, GitHubDeviceSignInTests).
/// </summary>
internal static class SetupAccountScenarios
{
    private const string Route = "/sindri/setup/accounts/github";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("setup-accounts", "code"), ("setup-accounts", "token-needed"), ("setup-accounts", "disconnected"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts a host with no gh login and a fake device endpoint; `code` adds the owner's client id in `setup.oauth.github`.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, string.Equals(scenario, "code", StringComparison.Ordinal) ? GoldenHost.ProfileYaml + "setup:\n  oauth:\n    github: Iv1.golden\n" : GoldenHost.ProfileYaml,
            extra: static services => services.Replace(ServiceDescriptor.Singleton<IGitHubCliLogin>(new SignedOut())).Replace(ServiceDescriptor.Singleton<IDeviceSignIn>(new FixedDevice())));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario)
    {
        _ = slug;
        var human = Human(host.Token.Value);
        return (await host.Http.Ask(string.Equals(scenario, "disconnected", StringComparison.Ordinal) ? Delete(Route, human) : Post(Route, json: null, human))).Body;
    }

    private sealed class SignedOut : IGitHubCliLogin
    {
        public ValueTask<string?> Login(CancellationToken cancellationToken) => ValueTask.FromResult<string?>(null);
    }

    // Hands out one fixed code; the poll waits until the host stops (the singleton's dispose cancels it).
    private sealed class FixedDevice : IDeviceSignIn
    {
        public ValueTask<Result<DeviceAuthorization>> Start(ConnectionPreset preset, string client, IReadOnlyList<string> scopes, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<DeviceAuthorization>>(new DeviceAuthorization("dc-golden", "ABCD-1234", new Uri("https://github.com/login/device"), KnownInstants.Epoch.AddMinutes(15), TimeSpan.FromSeconds(5)));

        public async ValueTask<Result<TokenSet>> Complete(ConnectionPreset preset, string client, DeviceAuthorization authorization, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            return Results.Failure<TokenSet>(SindriErrors.ConnectionSignInTimeout.With("never reached"));
        }
    }
}
