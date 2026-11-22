using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Delegations;
using Aw.Meta.Core.Ports;
using Aw.Meta.Features.Delegate;
using Aw.Meta.Features.Delegated;
using Aw.Meta.Features.DelegateDrop;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Delegate;

// The delegate and delegated services over one MetaWorld, with gh scripted to file issue #77.
internal sealed class DelegateWorld
{
    internal const string Created = "https://github.com/jthorare/agentic-workflow/issues/77";

    internal MetaWorld World { get; } = new();

    internal DelegationLedger Ledger => new(World.Values);

    // beatValues: the store the delivery reads the lane hook beats from; null for the world's own.
    internal DelegateService Service(IKeyValueStore? beatValues = null) => new(
        World.Config,
        World.Repo,
        World.LaneFiles,
        Ledger,
        World.Log,
        new LaneDelivery(World.LaneFiles, World.LaneMail, World.Messages, beatValues is null ? World.Beats : new LaneBeats(beatValues, World.Delay), World.Panes, World.Clock),
        World.Clock);

    internal DelegateDropService Dropping() => new();

    internal DelegatedService Listing() => new(Ledger, World.Messages);

    internal async Task Ready(DelegationConfig? delegation = null)
    {
        await World.Ready();
        if (delegation is not null)
        {
            _ = await World.Config.Save(new TeamConfig("t1", "/work", ["meta", "api", "ui"], "meta") { Delegation = delegation }, CancellationToken.None);
        }

        _ = World.Runner.When("issue create", Created + "\n");
    }

    internal static DelegateRequest Request(string? lane = "api", int? issue = null) =>
        new(lane, "Owner wants a dark theme toggle", "Add a toggle for the dark theme in Settings.", issue);

    internal async Task<DelegateResponse> Delegate(DelegateRequest request)
    {
        var result = await Service().Handle(request, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        return result.Value;
    }

    internal async Task<string> Refused(DelegateRequest request)
    {
        var result = await Service().Handle(request, CancellationToken.None);
        Assert.True(result.IsError);
        return result.Error.Code;
    }

    internal async Task<IReadOnlyList<Delegation>> Recorded() => (await Ledger.All(CancellationToken.None)).Value;

    internal IEnumerable<string> GhLines(string verb) => World.Runner.Lines.Where(l => l.Contains("issue " + verb, StringComparison.Ordinal));
}
