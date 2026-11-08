using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// GET /sindri/items/{id}/scope: an item's anchors and map version from its approved scoping packet. The packet's run area is a fake that serves the
/// lenses.json a scenario puts there (the real store hashes with aw-packets); the approval is the ledger row the scoping freeze will append.
/// </summary>
internal static class ItemScopeScenarios
{
    private const string Address = "ENG-1/scoping/1";
    private const string Hash = "sha256:0000000000000000000000000000000000000000000000000000000000000001";
    private const string NoSuchItem = "ITM-000000000000000000000000000000ff";

    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("items-id-scope", "scope-found"), ("items-id-scope", "scope-mixed-versions"), ("items-id-scope", "no-scope"), ("items-id-scope", "not-found"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Starts the item scenarios' host with the packet store replaced by <see cref="PacketStore"/>.</summary>
    public static Task<GoldenHost> Start(CancellationToken ct) =>
        GoldenHost.Start(temporary: true, ct, ItemScenarios.ProfileYaml, extra: static services => _ = services.Replace(ServiceDescriptor.Singleton<IScopePacketStore>(new PacketStore())));

    /// <summary>Runs the scenario against <paramref name="host"/> and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        if (scenario == "not-found")
        {
            return (await host.Http.Ask(Get($"/sindri/items/{NoSuchItem}/scope", ItemScenarios.Human(host)))).Body;
        }

        var id = await ItemScenarios.Create(host, "Try the queue", "demo");
        if (scenario != "no-scope")
        {
            var lenses = scenario == "scope-found"
                ? Lenses("v7@abc1234:feature:tabs.tab-manager,code:src/tabs.ts", "v7@abc1234:feature:tabs.tab-manager", "v7@abc1234:code:src/tabs-test.ts", "v7@abc1234:code:src/tabs.ts")
                : Lenses("v7@abc1234:feature:tabs.tab-manager", "v7@abc1234:feature:tabs.tab-manager", "v8@def5678:feature:tabs.tab-manager,code:src/sync.ts", "v7@abc1234:");
            ((PacketStore)host.App.Services.GetRequiredService<IScopePacketStore>()).Put(WorkflowInstanceId.Of(InstanceAddress.From(Address)), Hash, lenses);
            var payload = new ScopingApproved(Hash, Address, ["S1"], ["AC-1"], Rounds: 1, Rescopes: 0);
            await ItemScenarios.AppendFor(host, id, EventTypes.ScopingApproved, payload, "scoping.approved:" + Hash, ct);
        }

        await ViewScenarios.CatchUp(host, ct);
        return (await host.Http.Ask(Get($"/sindri/items/{id}/scope", ItemScenarios.Human(host)))).Body;
    }

    private static string Lenses(string code, string product, string related, string history) =>
        $$"""{"code":{{Lens(code)}},"product":{{Lens(product)}},"related":{{Lens(related)}},"history":{{Lens(history)}}}""";

    /// <summary>A lens entry: "v3@abc1234:key,key" is the map version, then the anchors.</summary>
    private static string Lens(string spec)
    {
        var parts = spec.Split(':', 2);
        var anchors = string.Join(',', parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(static a => $"\"{a}\""));
        return $$"""{"anchors":[{{anchors}}],"depth":2,"mapVersion":"{{parts[0]}}","foundCount":0}""";
    }

    /// <summary>Run areas by instance: the packet's hash and its lenses.json.</summary>
    private sealed class PacketStore : IScopePacketStore
    {
        private readonly Dictionary<WorkflowInstanceId, (string Hash, string Lenses)> _packets = [];

        public void Put(WorkflowInstanceId instance, string hash, string lenses) => _packets[instance] = (hash, lenses);

        public ValueTask<Result<string>> Hash(WorkflowInstanceId instance, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<string>>(_packets.TryGetValue(instance, out var packet) ? packet.Hash : SindriErrors.ItemNotFound.With("no packet"));

        public ValueTask<Result<string>> ReadFile(WorkflowInstanceId instance, string name, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<string>>(_packets.TryGetValue(instance, out var packet) && name == "lenses.json" ? packet.Lenses : SindriErrors.ItemNotFound.With("no file"));
    }
}
