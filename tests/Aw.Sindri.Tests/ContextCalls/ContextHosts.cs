using System.Net;
using Aw.Context.Core.Glossary;
using Aw.Context.Testing.Fixtures;
using Aw.Context.Testing.Glossary;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Started hosts whose approved profile maps the fixture repo, for the context surfaces' tests (HTTP and MCP).</summary>
internal static class ContextHosts
{
    /// <summary>The profile line that keeps a test host from asking the box's agent CLIs to name features and concepts (a host that fakes its own model opts in with 'labeling: true').</summary>
    internal const string NoLabeling = "  labeling: { enabled: false }\n";

    /// <summary>A host with no code index of its own reads no symbol vectors from the box's.</summary>
    /// <param name="services">The host's services.</param>
    internal static void NoCodeIndex(IServiceCollection services)
    {
        _ = services.RemoveAll<ISymbolVectors>();
        _ = services.AddSingleton<ISymbolVectors>(new FakeSymbolVectors());
    }

    /// <summary>A host whose profile maps <paramref name="repo"/> as split-panes, approved by the human, with one context-docs-refresh run done.</summary>
    /// <param name="repo">The fixture repo.</param>
    /// <param name="sessions">'context.sessions.enabled'.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <param name="extra">More service registrations, or null.</param>
    /// <param name="name">The repo's name in the profile.</param>
    /// <param name="contextExtra">More lines under 'context:' (two-space indented, each ending in a newline), or null.</param>
    /// <param name="labeling">True to leave 'context.labeling' on (the test fakes its own model).</param>
    /// <param name="profileExtra">More top-level keys after 'context:' (unindented, each line ending in a newline), or null.</param>
    /// <returns>The started host.</returns>
    internal static async Task<GoldenHost> Approved(FixtureRepo repo, bool sessions, CancellationToken ct, Action<IServiceCollection>? extra = null, string name = "split-panes", string? contextExtra = null, bool labeling = false, string? profileExtra = null) =>
        await Approved(repo.Path, sessions, ct, extra, name, contextExtra, labeling, profileExtra);

    /// <summary>A host whose profile maps the repo at <paramref name="repoPath"/> as <paramref name="name"/>, approved by the human, with one context-docs-refresh run done.</summary>
    /// <param name="repoPath">The repo's working tree.</param>
    /// <param name="sessions">'context.sessions.enabled'.</param>
    /// <param name="ct">Cancels the start.</param>
    /// <param name="extra">More service registrations, or null.</param>
    /// <param name="name">The repo's name in the profile.</param>
    /// <param name="contextExtra">More lines under 'context:' (two-space indented, each ending in a newline), or null.</param>
    /// <param name="labeling">True to leave 'context.labeling' on (the test fakes its own model).</param>
    /// <param name="profileExtra">More top-level keys after 'context:' (unindented, each line ending in a newline), or null.</param>
    /// <returns>The started host.</returns>
    internal static async Task<GoldenHost> Approved(string repoPath, bool sessions, CancellationToken ct, Action<IServiceCollection>? extra = null, string name = "split-panes", string? contextExtra = null, bool labeling = false, string? profileExtra = null)
    {
        var yaml = $"schemaVersion: 1\nuser: golden\nrepos:\n  - {{ name: {name}, path: \"{repoPath}\" }}\n"
            + $"context:\n  docs: {{ globs: [\"docs/**/*.md\"] }}\n  sessions: {{ enabled: {(sessions ? "true" : "false")} }}\n" + (labeling ? string.Empty : NoLabeling) + contextExtra + profileExtra;
        var host = await GoldenHost.Start(temporary: true, ct, yaml, extra: services => { NoCodeIndex(services); extra?.Invoke(services); });
        var hash = (await host.LiveHash(ct)).Value;
        var approved = await host.Http.Ask(Post("/sindri/profile/approve", await ApiScenarios.Confirmed(host, hash), Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        await using var scope = host.App.Services.CreateAsyncScope();
        var refresh = scope.ServiceProvider.GetServices<IScheduledService>().Single(s => s.Identity().Value == "context-docs-refresh");
        var grant = host.App.Services.GetRequiredService<HeldLease>().Current!;   // why: the started host holds the lease
        Assert.True((await refresh.Run(new ScheduledRun(grant, DateTimeOffset.UnixEpoch), ct)).IsSuccess);
        return host;
    }
}
