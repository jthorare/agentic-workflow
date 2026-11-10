using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.CreateItem;

/// <summary>Creating an item (API spec §3.2): 'item.created' keyed by the new id, the repo from the body or the session header.</summary>
public sealed class CreateItemTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CreateItemService Service() => new(_h.Profiles, _h.Held, _h.Store, _h.Events, _h.Ids, _h.Callers);

    private async Task Approve(params string[] repos)
    {
        var loaded = SliceHarness.Profile();
        _ = await _h.Profiles.Save(loaded with { Profile = loaded.Profile with { Repos = [.. repos.Select(r => new RepoEntry(r, "/repos/" + r))] } }, Token);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task CreateItem_appends_item_created_with_a_new_item_id()
    {
        _ = await _h.HoldLease();
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(A.CreateItemRequest().WithTitle("Try the queue").WithBody("details").Build(), Token);

        Assert.True(result.IsSuccess);
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.ItemCreated, recorded.Envelope.Type);
        Assert.Equal(recorded.Sequence, result.Value.Accepted.Sequence);
        Assert.Equal(result.Value.ItemId.Value, recorded.Item);
        Assert.Equal($"item.created:{result.Value.ItemId.Value}", recorded.IdempotencyKey);
        var payload = _h.Events.Payload<ItemCreated>(recorded).Value;
        Assert.Equal(("Try the queue", "details", result.Value.ItemId), (payload.Title, payload.Body, payload.Item));
        Assert.Equal(payload.CommandId, result.Value.Accepted.CommandId);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Link_is_recorded()
    {
        _ = await _h.HoldLease();
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());
        var link = RecordKey.From("linear:issue/abc-1");

        var result = await Service().Handle(A.CreateItemRequest().WithLink(link).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(link, _h.Events.Payload<ItemCreated>(Assert.Single(_h.Store.Events)).Value.Link);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Repo_defaults_to_the_session_header()
    {
        _ = await _h.HoldLease();
        await Approve("web");
        using var entered = _h.Callers.Enter(A.Caller().Human().InRepo("web").Build());

        var result = await Service().Handle(A.CreateItemRequest().Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(RepoName.From("web"), _h.Events.Payload<ItemCreated>(Assert.Single(_h.Store.Events)).Value.Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_body_repo_wins_over_the_header()
    {
        _ = await _h.HoldLease();
        await Approve("web", "api");
        using var entered = _h.Callers.Enter(A.Caller().Human().InRepo("web").Build());

        var result = await Service().Handle(A.CreateItemRequest().WithRepo(RepoName.From("api")).Build(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(RepoName.From("api"), _h.Events.Payload<ItemCreated>(Assert.Single(_h.Store.Events)).Value.Repo);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Unknown_repo_is_invalid_and_records_nothing()
    {
        _ = await _h.HoldLease();
        await Approve("web");
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(A.CreateItemRequest().WithRepo(RepoName.From("nope")).Build(), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_header_repo_outside_the_profile_says_what_to_do()
    {
        _ = await _h.HoldLease();
        await Approve("web");
        using var entered = _h.Callers.Enter(A.Caller().Human().InRepo("checkout").Build());

        var result = await Service().Handle(A.CreateItemRequest().WithRepo(null).Build(), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Contains("this checkout, checkout, isn't a repo of the profile (fix: pass --repo <name>, or add it to the profile)", result.Error.Message, StringComparison.Ordinal);
        Assert.Empty(_h.Store.Events);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_a_lease_it_is_not_running()
    {
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(A.CreateItemRequest().Build(), Token);

        Assert.Equal(SindriErrors.NotRunning.Code, result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\u0007title")]
    public void An_empty_or_control_character_title_is_invalid(string title)
    {
        var result = new CreateItemValidator().Validate(A.CreateItemRequest().WithTitle(title).Build());

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Title_and_body_limits_are_checked_after_trimming()
    {
        var validator = new CreateItemValidator();

        Assert.True(validator.Validate(A.CreateItemRequest().WithTitle("  " + new string('x', 200) + "  ").Build()).IsSuccess);
        Assert.True(validator.Validate(A.CreateItemRequest().WithTitle(new string('x', 201)).Build()).IsError);
        Assert.True(validator.Validate(A.CreateItemRequest().WithBody(new string('x', 4000)).Build()).IsSuccess);
        Assert.True(validator.Validate(A.CreateItemRequest().WithBody(new string('x', 4001)).Build()).IsError);
        Assert.True(validator.Validate(A.CreateItemRequest().WithBody("line one\nline two\ttabbed").Build()).IsSuccess);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_run_link_makes_one_item_and_a_warning()
    {
        _ = await _h.HoldLease();
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());
        var service = new CreateItemService(_h.Profiles, _h.Held, new LinkRefusingLedger(_h.Store), _h.Events, _h.Ids, _h.Callers);
        var run = RecordKey.From("sindri:run/nightly@1:1");

        var result = await service.Handle(A.CreateItemRequest().WithTitle("Try the queue").Build() with { RunRecord = run }, Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);   // success: the node doesn't fail, so nothing retries it
        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(EventTypes.ItemCreated, recorded.Envelope.Type);
        Assert.Contains("nightly@1:1", result.Value.Warning, StringComparison.Ordinal);
        Assert.DoesNotContain("#", result.Value.Warning, StringComparison.Ordinal);   // product words, no issue number
    }
}
