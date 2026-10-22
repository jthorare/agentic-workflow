using Aw.Sindri.Core.Setup;
using Aw.Sindri.Host;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of 'POST /setup/repos' (setup-once spec §5.1, §8): the cheap checks answer with real codes, a session is refused, a good folder starts the job. The folders are a fake git's
/// (/work/demo is a checkout), because the real adapter reads this machine; the real adapter is tested apart (GitRepoInspectorTests).
/// </summary>
internal static class SetupRepoScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("setup-repos", "started"), ("setup-repos", "refused-session"), ("setup-repos", "not-a-checkout"), ("setup-repos", "not-absolute"),
        ("setup-repos", "already-added"), ("setup-repos", "name-taken"), ("setup-repos", "bad-name"),
        ("setup-mcp", "not-in-profile"), ("setup-mcp", "refused-session"),   // POST /setup/mcp (§5.4): a repo the approved profile does not name is 409, a session is refused
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts a host whose git knows /work/demo and /work/other; the profile names /work/taken as 'taken' for the two scenarios that need an existing repo.</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, string.Equals(scenario, "already-added", StringComparison.Ordinal) || string.Equals(scenario, "name-taken", StringComparison.Ordinal) ? GoldenHost.ProfileYaml + "repos:\n  - name: demo\n    path: /work/demo\n" : GoldenHost.ProfileYaml,
            extra: services => services.Replace(ServiceDescriptor.Singleton<IRepoInspector>(Inspector())));

    /// <summary>The fake git these goldens and the route tests share.</summary>
    public static FakeRepoInspector Inspector() => new FakeRepoInspector().Has("/work/demo").Has("/work/other");

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario)
    {
        var human = Human(host.Token.Value);
        const string route = "/sindri/setup/repos";
        if (string.Equals(slug, "setup-mcp", StringComparison.Ordinal))
        {
            return await Mcp(host, scenario);
        }

        return (await host.Http.Ask(scenario switch
        {
            "started" => Post(route, "{\"path\":\"/work/demo\"}", human),
            "refused-session" => Post(route, "{\"path\":\"/work/demo\"}", [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")]),
            "not-a-checkout" => Post(route, "{\"path\":\"/work/missing\"}", human),
            "not-absolute" => Post(route, "{\"path\":\"demo\"}", human),
            "already-added" => Post(route, "{\"path\":\"/work/demo\"}", human),
            "name-taken" => Post(route, "{\"path\":\"/work/other\",\"name\":\"Demo\"}", human),
            "bad-name" => Post(route, "{\"path\":\"/work/demo\",\"name\":\"my repo\"}", human),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such add-repo scenario"),
        })).Body;
    }

    private static async Task<string> Mcp(GoldenHost host, string scenario)
    {
        const string route = "/sindri/setup/mcp";
        return (await host.Http.Ask(scenario switch
        {
            "not-in-profile" => Post(route, "{\"repo\":\"demo\"}", Human(host.Token.Value)),
            "refused-session" => Post(route, "{\"repo\":\"demo\"}", [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli")]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such setup-mcp scenario"),
        })).Body;
    }
}
