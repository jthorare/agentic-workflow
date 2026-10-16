using Aw.Kernel.Ids;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.Requests;

/// <summary>The views the request, decision and needs-you queries read, seeded through the real projections at stepped times (a minute apart).</summary>
internal sealed class RequestQueryHarness
{
    private int _next;

    public ViewsHarness Views { get; } = new();

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public IDisposable Human() => Views.Slice.Callers.Enter(A.Caller().Human().Build());

    public IDisposable Session(string? repo) => Views.Slice.Callers.Enter(repo is null ? A.Caller().Session().Build() : A.Caller().Session().InRepo(repo).Build());

    public static ItemId Item(char digit) => ItemId.From("ITM-" + new string(digit, 32));

    public async Task CreateItem(ItemId item, string? repo = null)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.ItemCreated, new ItemCreated(Views.Slice.Ids.New<CommandId>(), item, "An item", repo is null ? null : RepoName.From(repo), Link: null, Body: null, Owner: null), item);
    }

    /// <summary>Files a request one minute after the last event; returns its decision (the n-th one is DEC-…n).</summary>
    public async Task<DecisionId> File(string text, RequestKind kind = RequestKind.Rescope, string? repo = null, ItemId? item = null)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        _next++;
        var (request, decision) = (RequestId.From("REQ-" + _next.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)), DecisionId.From("DEC-" + _next.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)));
        var filed = new RequestFiled(Views.Slice.Ids.New<CommandId>(), request, decision, kind, item, repo is null ? null : RepoName.From(repo), text);
        if (item is { } about)
        {
            await Views.Fold(EventTypes.RequestFiled, filed, about);
        }
        else
        {
            await Views.Fold(EventTypes.RequestFiled, filed);
        }

        return decision;
    }

    public async Task Decide(DecisionId decision, string choice, string note = "ok", string by = "human:ui")
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.DecisionRecorded, new DecisionRecorded(Views.Slice.Ids.New<CommandId>(), decision, DecisionOption.From(choice), note, by));
    }

    public async Task Close(ItemId item)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(Views.Slice.Ids.New<CommandId>(), item, ItemState.NeedsDecision, ItemState.Cancelled, 3, Reason: null), item);
    }

    public async Task Ran(string service, ServiceOutcome outcome, string? code = null)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.ServiceRan, new ServiceRan(Views.Slice.Ids.New<CommandId>(), ServiceName.From(service), Views.Slice.Clock.GetUtcNow(), outcome, code));
    }

    public async Task Drift()
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.ProfileChecked, new ProfileChecked(Views.Slice.Ids.New<CommandId>(), ProfileHash.From(new string('a', 64)), ProfileHash.From(new string('b', 64)),
            Drifted: true, ["services.index"], [ServiceName.From("heartbeat")]));
    }

    public async Task Start(ItemId item)
    {
        Views.Slice.Clock.Advance(TimeSpan.FromMinutes(1));
        await Views.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(Views.Slice.Ids.New<CommandId>(), item, ItemState.New, ItemState.Running, 2, Reason: null), item);
    }
}
