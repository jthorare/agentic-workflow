using System.Text.Json;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Tests.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// The goldens of the memory routes the Memory page reads and writes (agent-first PR 18a): the list, a create, a recall by meaning and by keywords (the model off), an unknown store,
/// and the fact writes (added, refreshed) and forgets (by id, by tag). The stores and facts are seeded through the routes themselves, over a fake embedder with a fixed vector per text,
/// at the host's fixed clock and sequential ids. Every request is the owner's, so the answers come unfenced.
/// </summary>
internal static class MemoryScenarios
{
    private const string Store = "team-notes";
    private const string Tuesdays = "Deploys go out on Tuesdays";
    private const string Rollbacks = "Rollbacks need the on-call's OK";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("memories", "list"), ("memories", "created"),
        ("memories-id-search", "semantic"), ("memories-id-search", "keyword"), ("memories-id-search", "not-found"),
        ("memories-id-facts", "written"), ("memories-id-facts", "refreshed"), ("memories-id-facts", "forgot"), ("memories-id-facts", "forgot-tag"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the scenario's host over a fake embedder (off for the keyword recall).</summary>
    public static Task<GoldenHost> Start(string scenario, CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, extra: services =>
        {
            var embedder = new FakeMemoryEmbedder { Off = scenario == "keyword" }
                .With(Tuesdays, 1f, 0f, 0f, 0f)
                .With("Deploys happen on Tuesdays", 1f, 0f, 0f, 0f)
                .With("deploys", 1f, 0f, 0f, 0f)
                .With(Rollbacks, 0f, 1f, 0f, 0f);
            _ = services.RemoveAll<IMemoryEmbedder>();
            _ = services.AddSingleton<IMemoryEmbedder>(embedder);
        });

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        await ViewScenarios.Approve(host, ct);
        var owner = Human(host.Token.Value);
        return (slug, scenario) switch
        {
            ("memories", "list") => await List(host, owner),
            ("memories", "created") => await Send(host, Post("/sindri/memories", """{"name":"team-notes","description":"What the team agreed on"}""", owner)),
            ("memories-id-search", "not-found") => await Send(host, Get("/sindri/memories/no-such-store/search?query=deploys", owner)),
            ("memories-id-search", _) => await Search(host, owner),
            ("memories-id-facts", "written") => await Written(host, owner),
            ("memories-id-facts", "refreshed") => await Refreshed(host, owner),
            ("memories-id-facts", "forgot") => await Forgot(host, owner),
            ("memories-id-facts", "forgot-tag") => await ForgotTag(host, owner),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "no such memory scenario"),
        };
    }

    private static async Task<string> List(GoldenHost host, (string Name, string Value)[] owner)
    {
        _ = await Seed(host, owner, Store, Tuesdays, "[\"deploys\"]");
        _ = await Seed(host, owner, "release-log", Rollbacks, "[]", create: true);
        _ = await Seed(host, owner, "release-log", Tuesdays, "[\"deploys\"]", create: false);
        return await Send(host, Get("/sindri/memories", owner));
    }

    private static async Task<string> Search(GoldenHost host, (string Name, string Value)[] owner)
    {
        _ = await Seed(host, owner, Store, Tuesdays, "[\"deploys\"]");
        _ = await Seed(host, owner, Store, Rollbacks, "[]", create: false);
        return await Send(host, Get($"/sindri/memories/{Store}/search?query=deploys", owner));
    }

    private static async Task<string> Written(GoldenHost host, (string Name, string Value)[] owner)
    {
        await Create(host, owner, Store);
        return await Send(host, Post($"/sindri/memories/{Store}/facts", $$"""{"text":"{{Tuesdays}}","tags":["deploys"]}""", owner));
    }

    private static async Task<string> Refreshed(GoldenHost host, (string Name, string Value)[] owner)
    {
        _ = await Seed(host, owner, Store, Tuesdays, "[\"deploys\"]");
        return await Send(host, Post($"/sindri/memories/{Store}/facts", """{"text":"Deploys happen on Tuesdays","tags":["deploys"]}""", owner));
    }

    private static async Task<string> Forgot(GoldenHost host, (string Name, string Value)[] owner)
    {
        var key = await Seed(host, owner, Store, Tuesdays, "[\"deploys\"]");
        return await Send(host, Delete($"/sindri/memories/{Store}/facts?fact={Uri.EscapeDataString(key)}", owner));
    }

    private static async Task<string> ForgotTag(GoldenHost host, (string Name, string Value)[] owner)
    {
        _ = await Seed(host, owner, Store, Tuesdays, "[\"deploys\"]");
        _ = await Seed(host, owner, Store, Rollbacks, "[]", create: false);
        return await Send(host, Delete($"/sindri/memories/{Store}/facts?tag=deploys", owner));
    }

    private static async Task Create(GoldenHost host, (string Name, string Value)[] owner, string name) =>
        Require(await host.Http.Ask(Post("/sindri/memories", $$"""{"name":"{{name}}"}""", owner)));

    /// <summary>Creates the store (unless told not to) and writes one fact; returns the new fact's key.</summary>
    private static async Task<string> Seed(GoldenHost host, (string Name, string Value)[] owner, string store, string text, string tagsJson, bool create = true)
    {
        if (create)
        {
            await Create(host, owner, store);
        }

        var written = await host.Http.Ask(Post($"/sindri/memories/{store}/facts", $$"""{"text":"{{text}}","tags":{{tagsJson}}}""", owner));
        Require(written);
        using var doc = JsonDocument.Parse(written.Body);
        return doc.RootElement.EnumerateObject().First(p => string.Equals(p.Name, "id", StringComparison.OrdinalIgnoreCase)).Value.GetString()!;
    }

    private static void Require(Callers.Answer answer) =>
        Assert.True(answer.Status == System.Net.HttpStatusCode.OK, $"seed: {answer.Status} {answer.Body}");

    private static async Task<string> Send(GoldenHost host, HttpRequestMessage request)
    {
        using (request)
        {
            return (await host.Http.Ask(request)).Body;
        }
    }
}
