using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Features.LineHoldout;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Challenges;
using Aw.Sindri.Testing.Prove;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.LineHoldout;

[Lane(TestLane.Unit)]
public sealed class ShowLineHoldoutTests
{
    private const string SetText = """{ "schemaVersion": 1, "phase": "p1", "scenarios": [ { "id": "P1-H1", "item": "AC-1", "kind": "api", "steps": ["open an expired link"], "expected": "answers 410" }, { "id": "P1-H2", "item": "AC-2", "kind": "ui", "steps": ["click save twice"], "expected": "one record" }, { "id": "P1-C", "item": null, "kind": "canary", "steps": [], "expected": "never", "phrase": "violet otter ledger 4417" } ] }""";

    private readonly InMemoryHoldoutStore _store = new();

    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private ShowLineHoldoutService Service() => new(_store, new InMemoryChallenges(_clock));

    [Fact]
    public async Task Without_an_answer_it_shows_counts_and_a_challenge_and_no_text()
    {
        _store.Put("T-7", "p1", SetText);

        var shown = await Service().Handle(new ShowLineHoldoutRequest("T-7", "p1"), Token);

        Expect.True(shown.IsSuccess);
        Expect.Equal(2, shown.Value.Total);
        Expect.Equal(0, shown.Value.Spent);
        Expect.True(shown.Value.Scenarios is null && shown.Value.Challenge is not null);
        Expect.Equal(0, (await _store.Spent("T-7", "p1", Token)).Value.Count);
    }

    [Fact]
    public async Task The_typed_confirmation_shows_the_text_without_the_canary_and_marks_what_it_showed_spent()
    {
        _store.Put("T-7", "p1", SetText);
        var service = Service();
        var challenge = (await service.Handle(new ShowLineHoldoutRequest("T-7", "p1"), Token)).Value.Challenge!;

        var shown = await service.Handle(new ShowLineHoldoutRequest("T-7", "p1", new ChallengeAnswer(challenge.Challenge, challenge.Action)), Token);

        Expect.True(shown.IsSuccess);
        Expect.Sequence(["P1-H1", "P1-H2"], shown.Value.Scenarios!.Select(static s => s.Id));
        Expect.Equal("open an expired link", shown.Value.Scenarios![0].Steps[0]);
        Expect.True(shown.Value.Challenge is null);
        Expect.Sequence(["P1-H1", "P1-H2"], (await _store.Spent("T-7", "p1", Token)).Value.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_scenario_spent_before_is_flagged_when_shown_again()
    {
        _store.Put("T-7", "p1", SetText);
        _ = await _store.MarkSpent("T-7", "p1", ["P1-H1"], Token);
        var service = Service();
        var preview = await service.Handle(new ShowLineHoldoutRequest("T-7", "p1"), Token);
        var challenge = preview.Value.Challenge!;

        var shown = await service.Handle(new ShowLineHoldoutRequest("T-7", "p1", new ChallengeAnswer(challenge.Challenge, challenge.Action)), Token);

        Expect.Equal(1, preview.Value.Spent);
        Expect.Sequence([true, false], shown.Value.Scenarios!.Select(static s => s.WasSpent));
    }

    [Fact]
    public async Task A_wrong_answer_shows_nothing_and_marks_nothing_spent()
    {
        _store.Put("T-7", "p1", SetText);
        var service = Service();
        var challenge = (await service.Handle(new ShowLineHoldoutRequest("T-7", "p1"), Token)).Value.Challenge!;

        var refused = await service.Handle(new ShowLineHoldoutRequest("T-7", "p1", new ChallengeAnswer(challenge.Challenge, "yes")), Token);

        Expect.True(refused.IsError);
        Expect.Equal(SindriErrors.AuthRefused.Code, refused.Error.Code);
        Expect.Equal(0, (await _store.Spent("T-7", "p1", Token)).Value.Count);
    }

    [Fact]
    public async Task A_phase_with_no_set_is_the_stores_error()
    {
        var shown = await Service().Handle(new ShowLineHoldoutRequest("T-7", "p9"), Token);

        Expect.True(shown.IsError);
        Expect.Equal("SND-BLOCK-005", shown.Error.Code);
    }
}
