using Aw.Sindri.Core.Events;
using Aw.Sindri.Core;
using Aw.Sindri.Features.CreateHandoffItem;
using Aw.Sindri.Features.CreateItem;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Handoff;

/// <summary>A handoff makes an item with the existing create-item handler, then keeps the whole payload in 'item.handoff' (#2258 PR 12).</summary>
public sealed class HandoffItemTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CreateHandoffItemService Service() =>
        new(new FixItemMaker(new CreateItemService(_h.Profiles, _h.Held, _h.Store, _h.Events, _h.Ids, _h.Callers)), _h.Held, _h.Store, _h.Events, _h.Callers);

    [Fact, Trait("Lane", "unit")]
    public async Task The_offer_payload_is_kept_in_item_handoff_even_when_the_prompt_exceeds_the_body_limit()
    {
        _ = await _h.HoldLease();
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());
        var prompt = new string('p', 10_000);
        var request = new CreateHandoffItemRequest("Fix the login timeout", "ci-bot", Offer: "offer-7", Branch: "fix/login", Prompt: prompt,
            Links: ["https://example.test/t/7"], Labels: ["bug"]);

        var result = await Service().Handle(request, Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(2, _h.Store.Events.Count);
        var created = _h.Events.Payload<ItemCreated>(_h.Store.Events[0]).Value;
        Assert.Equal(EventTypes.ItemCreated, _h.Store.Events[0].Envelope.Type);
        Assert.Equal(4000, created.Body!.Length);
        var handoff = _h.Store.Events[1];
        Assert.Equal(EventTypes.ItemHandoff, handoff.Envelope.Type);
        Assert.Equal("handoff:ci-bot", handoff.Envelope.Actor);
        Assert.Equal(result.Value.ItemId.Value, handoff.Item);
        Assert.Equal(handoff.Sequence, result.Value.Accepted.Sequence);
        var payload = _h.Events.Payload<ItemHandoff>(handoff).Value;
        Assert.Equal(prompt, payload.Prompt);
        Assert.Equal(("offer-7", "ci-bot", "fix/login", result.Value.ItemId), (payload.Offer, payload.Source, payload.Branch, payload.Item));
        Assert.Equal(["https://example.test/t/7"], payload.Links);
        Assert.Equal(["bug"], payload.Labels);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_an_offer_id_the_item_id_stands_in()
    {
        _ = await _h.HoldLease();
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new CreateHandoffItemRequest("Try it", "local"), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(result.Value.ItemId.Value, result.Value.Offer);
        Assert.Equal(result.Value.ItemId.Value, _h.Events.Payload<ItemHandoff>(_h.Store.Events[1]).Value.Offer);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("Upper")]
    [InlineData("has space")]
    [InlineData("")]
    public void A_source_outside_a_to_z_digits_dot_underscore_and_dash_is_invalid(string source)
    {
        var result = new CreateHandoffItemValidator().Validate(new CreateHandoffItemRequest("Title", source));

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_prompt_over_65536_characters_is_invalid_and_a_plain_handoff_is_not()
    {
        var validator = new CreateHandoffItemValidator();

        Assert.True(validator.Validate(new CreateHandoffItemRequest("Title", "ci-bot", Prompt: new string('p', 65_536))).IsSuccess);
        Assert.Equal(SindriErrors.InvalidInput.Code, validator.Validate(new CreateHandoffItemRequest("Title", "ci-bot", Prompt: new string('p', 65_537))).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_blank_title_records_nothing()
    {
        _ = await _h.HoldLease();
        using var entered = _h.Callers.Enter(A.Caller().Human().Build());

        var result = await Service().Handle(new CreateHandoffItemRequest(" ", "ci-bot"), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
        Assert.Empty(_h.Store.Events);
    }
}
