using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.FileRequest;
using Aw.Sindri.Features.Items;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Views;

namespace Aw.Sindri.Tests.FileRequest;

/// <summary>Filing a request (API spec §3.2, P4 plan Unit D): one 'request.filed' keyed by the new request, the repo from the item or the session header, the caps for a session, the text scrubbed.</summary>
[Lane(TestLane.Unit)]
public sealed class FileRequestTests
{
    private static readonly ItemAlias _alias = ItemAlias.From("ENG-142");
    private readonly ViewsHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public FileRequestTests()
    {
        _ = _h.Slice.Profiles.Save(ProfileOfRepos("web", "api", "a", "b", "c", "d"), CancellationToken.None).AsTask().Result;
    }

    private static LoadedProfile ProfileOfRepos(params string[] repos) =>
        SliceHarness.Profile() with { Profile = SliceHarness.Profile().Profile with { Repos = [.. repos.Select(r => new RepoEntry(r, "/work/" + r))] } };

    private FileRequestService Service(ITextScrubber? scrubber = null) =>
        new(new RequestTarget(new ViewItemResolver(_h.Views), _h.Views, _h.Slice.Profiles), _h.Slice.Store, _h.Slice.Held, _h.Slice.Events, _h.Slice.Ids, scrubber ?? new PassThroughScrubber(), _h.Slice.Callers);

    private IDisposable Session(string repo = "web") => _h.Slice.Callers.Enter(A.Caller().Session().InRepo(repo).Build());

    private IDisposable Human() => _h.Slice.Callers.Enter(A.Caller().Human().Build());

    private async Task Item(ItemId id, string? repo)
    {
        _ = await _h.Slice.HoldLease();
        await _h.Fold(EventTypes.ItemCreated, new ItemCreated(_h.Slice.Ids.New<CommandId>(), id, "Ship it", repo is null ? null : RepoName.From(repo), Link: null, Body: null, Owner: null), id);
    }

    private async Task Open(int count, string? repo)
    {
        for (var n = 0; n < count; n++)
        {
            await _h.Fold(EventTypes.RequestFiled, new RequestFiled(_h.Slice.Ids.New<CommandId>(), RequestId.New(_h.Slice.Ids), DecisionId.New(_h.Slice.Ids),
                RequestKind.Note, Item: null, repo is null ? null : RepoName.From(repo), "earlier"));
        }
    }

    [Fact]
    public async Task FileRequest_from_session_appends_one_event_with_repo()
    {
        _ = await _h.Slice.HoldLease();
        using var entered = Session("web");

        var result = await Service().Handle(A.FileRequestRequest().OfKind(RequestKind.Note).WithText("Heads up.").Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Expect.Equal(EventTypes.RequestFiled, recorded.Envelope.Type);
        Expect.Equal(recorded.Sequence, result.Value.Accepted.Sequence);
        Expect.Equal($"request.filed:{result.Value.RequestId.Value}", recorded.IdempotencyKey);
        Assert.Null(recorded.Item);
        Expect.Equal("session:http", recorded.Envelope.Actor);
        var payload = _h.Slice.Events.Payload<RequestFiled>(recorded).Value;
        Expect.Equal((result.Value.RequestId, result.Value.DecisionId, RequestKind.Note), (payload.Request, payload.Decision, payload.Kind));
        Expect.Equal((RepoName.From("web"), "Heads up."), (payload.Repo!.Value, payload.Text));
        Expect.Equal(payload.CommandId, result.Value.Accepted.CommandId);
        Assert.Null(payload.Item);
    }

    [Fact]
    public async Task A_session_in_a_repo_outside_the_profile_is_refused_and_records_nothing()
    {
        _ = await _h.Slice.HoldLease();
        using var entered = Session("gamma");

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Expect.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Contains("'gamma' isn't a repo of the approved profile", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task A_request_about_an_item_takes_the_items_repo_and_names_the_item()
    {
        await Item(A.FirstItemId, "api");
        using var entered = Human();

        var result = await Service().Handle(A.FileRequestRequest().AboutItem(ItemRef.To(A.FirstItemId)).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Expect.Equal(A.FirstItemId.Value, recorded.Item);
        var payload = _h.Slice.Events.Payload<RequestFiled>(recorded).Value;
        Expect.Equal((A.FirstItemId, RepoName.From("api")), (payload.Item!.Value, payload.Repo!.Value));
    }

    [Fact]
    public async Task An_item_can_be_named_by_alias()
    {
        await Item(A.FirstItemId, repo: null);
        await _h.Fold(EventTypes.ItemLinked, new ItemLinked(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, RecordKey.From("linear:issue/abc-123"), _alias), A.FirstItemId);
        using var entered = Human();

        var result = await Service().Handle(A.FileRequestRequest().AboutItem(ItemRef.To(_alias)).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Expect.Equal(A.FirstItemId.Value, Assert.Single(_h.Slice.Store.Events).Item);
    }

    [Fact]
    public async Task A_human_with_no_header_files_a_request_with_no_repo()
    {
        _ = await _h.Slice.HoldLease();
        using var entered = Human();

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Null(_h.Slice.Events.Payload<RequestFiled>(Assert.Single(_h.Slice.Store.Events)).Value.Repo);
    }

    [Fact]
    public async Task FileRequest_one_past_the_repo_cap_is_SND_REQ_002()
    {
        _ = await _h.Slice.HoldLease();
        await Open(RequestRules.MaxOpenPerRepo, "web");
        var before = _h.Slice.Store.Events.Count;
        using var entered = Session("web");

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Expect.Equal(SindriErrors.RequestsRateLimited.Code, result.Error.Code);
        Expect.Equal(before, _h.Slice.Store.Events.Count);
    }

    [Fact]
    public async Task The_last_under_the_repo_cap_is_still_accepted_and_other_repos_have_their_own_count()
    {
        _ = await _h.Slice.HoldLease();
        await Open(RequestRules.MaxOpenPerRepo - 1, "web");
        await Open(RequestRules.MaxOpenPerRepo - 1, "api");
        using var entered = Session("web");

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }

    [Fact]
    public async Task One_past_the_total_cap_is_SND_REQ_002()
    {
        _ = await _h.Slice.HoldLease();
        await Open(RequestRules.MaxOpenPerRepo - 1, "a");
        await Open(RequestRules.MaxOpenPerRepo - 1, "b");
        await Open(RequestRules.MaxOpenTotal - (2 * (RequestRules.MaxOpenPerRepo - 1)), "c");
        using var entered = Session("d");

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Expect.Equal(SindriErrors.RequestsRateLimited.Code, result.Error.Code);
    }

    [Fact]
    public async Task A_decided_request_no_longer_counts_as_open()
    {
        _ = await _h.Slice.HoldLease();
        await Open(19, "web");
        var request = RequestId.New(_h.Slice.Ids);
        var decision = DecisionId.New(_h.Slice.Ids);
        await _h.Fold(EventTypes.RequestFiled, new RequestFiled(_h.Slice.Ids.New<CommandId>(), request, decision, RequestKind.Note, Item: null, RepoName.From("web"), "x"));
        await _h.Fold(EventTypes.DecisionRecorded, new DecisionRecorded(_h.Slice.Ids.New<CommandId>(), decision, DecisionOption.From("decline"), Note: null, "human:cli"));
        using var entered = Session("web");

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }

    [Fact]
    public async Task A_human_is_not_capped()
    {
        _ = await _h.Slice.HoldLease();
        await Open(50, "web");
        using var entered = Human();

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }

    [Fact]
    public async Task FileRequest_against_other_repo_item_files_it_against_that_repo()
    {
        await Item(A.FirstItemId, "api");
        using var entered = Session("web");

        var result = await Service().Handle(A.FileRequestRequest().AboutItem(ItemRef.To(A.FirstItemId)).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
    }

    [Fact]
    public async Task A_closed_item_is_SND_ITEM_409_already_done()
    {
        await Item(A.FirstItemId, "web");
        await _h.Fold(EventTypes.ItemStateChanged, new ItemStateChanged(_h.Slice.Ids.New<CommandId>(), A.FirstItemId, ItemState.New, ItemState.Done, 2, Reason: null), A.FirstItemId);
        using var entered = Session("web");

        var result = await Service().Handle(A.FileRequestRequest().AboutItem(ItemRef.To(A.FirstItemId)).Build(), Token);

        Expect.Equal(SindriErrors.ItemConflict.Code, result.Error.Code);
        Assert.Contains("already done", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Slice.Store.Events);
    }

    [Fact]
    public async Task FileRequest_text_is_scrubbed_in_the_ledger()
    {
        _ = await _h.Slice.HoldLease();
        using var entered = Human();

        var result = await Service(new MaskScrubber("hunter2")).Handle(A.FileRequestRequest().WithText("Use password hunter2 for the db").Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var recorded = Assert.Single(_h.Slice.Store.Events);
        Assert.DoesNotContain("hunter2", recorded.Envelope.Payload, StringComparison.Ordinal);
        Expect.Equal("Use password [masked] for the db", _h.Slice.Events.Payload<RequestFiled>(recorded).Value.Text);
    }

    [Fact]
    public async Task A_secret_near_the_limit_is_cut_after_scrubbing()
    {
        _ = await _h.Slice.HoldLease();
        using var entered = Human();
        var text = new string('a', RequestRules.TextMax - 10) + "hunter2" + new string('b', 3);

        var result = await Service(new MaskScrubber("hunter2")).Handle(A.FileRequestRequest().WithText(text).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var stored = _h.Slice.Events.Payload<RequestFiled>(Assert.Single(_h.Slice.Store.Events)).Value.Text;
        Assert.DoesNotContain("hunter", stored, StringComparison.Ordinal);
        Assert.True(stored.Length <= RequestRules.TextMax);
    }

    [Fact]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var entered = Human();

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Expect.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Fact]
    public async Task Without_a_caller_it_is_refused()
    {
        _ = await _h.Slice.HoldLease();

        var result = await Service().Handle(A.FileRequestRequest().Build(), Token);

        Expect.Equal(SindriErrors.AuthRefused.Code, result.Error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void The_validator_refuses_empty_text(string text) =>
        Expect.Equal(SindriErrors.RequestInvalid.Code, new FileRequestValidator().Validate(A.FileRequestRequest().WithText(text).Build()).Error.Code);

    [Fact]
    public void The_validator_refuses_more_than_65536_characters_and_takes_exactly_65536()
    {
        Expect.Equal(SindriErrors.RequestInvalid.Code, new FileRequestValidator().Validate(A.FileRequestRequest().WithText(new string('x', RequestRules.TextMax + 1)).Build()).Error.Code);
        Assert.True(new FileRequestValidator().Validate(A.FileRequestRequest().WithText(new string('x', RequestRules.TextMax)).Build()).IsSuccess);
    }

    [Fact]
    public void The_validator_refuses_a_kind_that_is_not_one() =>
        Expect.Equal(SindriErrors.RequestInvalid.Code, new FileRequestValidator().Validate(A.FileRequestRequest().OfKind((RequestKind)9).Build()).Error.Code);

    private sealed class MaskScrubber(string secret) : ITextScrubber
    {
        public string Scrub(string text) => text.Replace(secret, "[masked]", StringComparison.Ordinal);
    }
}
