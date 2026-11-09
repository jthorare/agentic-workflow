using Aw.Sindri.Core;
using Aw.Sindri.Core.Handoff;
using Xunit;

namespace Aw.Sindri.Testing.Handoff;

/// <summary>
/// The contract every <see cref="IInboxClient"/> honours: list pages the owner's unclaimed offers with every field intact, exactly one claim wins
/// (the loser gets SND-CONN-202 and the offer leaves the list), an unknown offer is SND-CONN-203, a result is taken once however often it is sent,
/// and a wrong key is SND-CONN-002 on all three calls.
/// </summary>
public abstract class InboxContract
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Runs list, claim and submit end to end over one implementation; the card's one-line proof.</summary>
    /// <param name="inbox">The implementation; the flow disposes it.</param>
    /// <returns>A task that ends when the flow has been checked.</returns>
    public static async Task ListClaimAndSubmit(InboxUnderTest inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        using var owned = inbox;
        inbox.Backing.Seed("me", Sample("o-1"));
        var target = inbox.Target("me");

        var listed = await inbox.Client.ListOffers(target, pageToken: null, Token);
        Expect.True(listed.IsSuccess);
        Expect.Equal("o-1", Expect.Only(listed.Value.Offers).Key);

        var claimed = await inbox.Client.Claim(target, "o-1", Token);
        Expect.True(claimed.IsSuccess);
        Expect.True(!string.IsNullOrEmpty(claimed.Value.ClaimKey));

        var result = new InboxResult(claimed.Value.ClaimKey, Succeeded: true, "done", [new HandoffLink("PR", "https://example.test/pr/1")]);
        var submitted = await inbox.Client.Submit(target, "o-1", result, Token);
        Expect.True(submitted.IsSuccess);
        Expect.Equal("done", Expect.Only(inbox.Backing.Submissions).Result.Markdown);
    }

    /// <summary>Makes the implementation under test, over a fake inbox with page size 2 and key 'inbox-key'.</summary>
    /// <returns>The implementation and its backing fake.</returns>
    protected abstract InboxUnderTest Create();

    /// <summary>Every field of an offer comes back as it was seeded.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_returns_every_field_of_the_offer()
    {
        using var inbox = Create();
        var offer = new Offer("o-1", "Fix the thing", "acme/web", "fix/thing", "Please fix it.", [new HandoffLink("Ticket", "https://example.test/t/1")], ["bug", "urgent"]);
        inbox.Backing.Seed("me", offer);

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Expect.True(listed.IsSuccess);
        var got = Expect.Only(listed.Value.Offers);
        Expect.Equal(offer.Key, got.Key);
        Expect.Equal(offer.Title, got.Title);
        Expect.Equal(offer.Repo, got.Repo);
        Expect.Equal(offer.Branch, got.Branch);
        Expect.Equal(offer.Prompt, got.Prompt);
        Expect.Sequence(offer.Links, got.Links);
        Expect.Sequence(offer.Labels, got.Labels);
        Expect.True(listed.Value.NextPageToken is null);
    }

    /// <summary>An offer without a branch or a prompt reads back with nulls.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_keeps_optional_fields_null()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", new Offer("o-1", "Bare", "acme/web", Branch: null, Prompt: null, Links: [], Labels: []));

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        var got = Expect.Only(listed.Value.Offers);
        Expect.True(got.Branch is null);
        Expect.True(got.Prompt is null);
        Expect.Equal(0, got.Links.Count);
        Expect.Equal(0, got.Labels.Count);
    }

    /// <summary>Three offers on pages of two arrive as two pages, in order, with no repeat.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_pages_with_a_token()
    {
        using var inbox = Create();
        foreach (var key in new[] { "o-1", "o-2", "o-3" })
        {
            inbox.Backing.Seed("me", Sample(key));
        }

        var first = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);
        Expect.Sequence(["o-1", "o-2"], first.Value.Offers.Select(static o => o.Key));
        Expect.True(first.Value.NextPageToken is not null);

        var second = await inbox.Client.ListOffers(inbox.Target("me"), first.Value.NextPageToken, Token);
        Expect.Sequence(["o-3"], second.Value.Offers.Select(static o => o.Key));
        Expect.True(second.Value.NextPageToken is null);
    }

    /// <summary>One owner never sees another owner's offers.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task List_is_per_owner()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", Sample("mine"));
        inbox.Backing.Seed("you", Sample("yours"));

        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Expect.Sequence(["mine"], listed.Value.Offers.Select(static o => o.Key));
    }

    /// <summary>The first claim wins; the second is SND-CONN-202; the claimed offer leaves the list.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Exactly_one_claim_wins()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", Sample("o-1"));

        var first = await inbox.Client.Claim(inbox.Target("me"), "o-1", Token);
        var second = await inbox.Client.Claim(inbox.Target("me"), "o-1", Token);
        var listed = await inbox.Client.ListOffers(inbox.Target("me"), pageToken: null, Token);

        Expect.True(first.IsSuccess);
        Expect.Equal("o-1", first.Value.Offer.Key);
        Expect.True(second.IsError);
        Expect.Equal(SindriErrors.InboxClaimLost.Code, second.Error.Code);
        Expect.Equal(0, listed.Value.Offers.Count);
    }

    /// <summary>An offer that was never there, or has expired, is SND-CONN-203.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Claiming_an_unknown_or_expired_offer_is_gone()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", Sample("o-1"));
        inbox.Backing.Expire("o-1");

        var unknown = await inbox.Client.Claim(inbox.Target("me"), "nope", Token);
        var expired = await inbox.Client.Claim(inbox.Target("me"), "o-1", Token);

        Expect.Equal(SindriErrors.InboxOfferGone.Code, unknown.Error.Code);
        Expect.Equal(SindriErrors.InboxOfferGone.Code, expired.Error.Code);
    }

    /// <summary>The same result sent twice is taken once.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Submit_is_idempotent_on_the_claim()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", Sample("o-1"));
        var claim = (await inbox.Client.Claim(inbox.Target("me"), "o-1", Token)).Value;
        var result = new InboxResult(claim.ClaimKey, Succeeded: false, "it failed: boom", []);

        var first = await inbox.Client.Submit(inbox.Target("me"), "o-1", result, Token);
        var again = await inbox.Client.Submit(inbox.Target("me"), "o-1", result, Token);

        Expect.True(first.IsSuccess);
        Expect.True(again.IsSuccess);
        var (_, taken) = Expect.Only(inbox.Backing.Submissions);
        Expect.True(!taken.Succeeded);
        Expect.Equal("it failed: boom", taken.Markdown);
    }

    /// <summary>A result that names a claim the inbox did not give is refused.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Submit_with_a_foreign_claim_is_refused()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", Sample("o-1"));
        _ = await inbox.Client.Claim(inbox.Target("me"), "o-1", Token);

        var submitted = await inbox.Client.Submit(inbox.Target("me"), "o-1", new InboxResult("someone-elses", Succeeded: true, "x", []), Token);

        Expect.True(submitted.IsError);
        Expect.Equal(SindriErrors.InboxSubmitFailed.Code, submitted.Error.Code);
        Expect.Equal(0, inbox.Backing.Submissions.Count);
    }

    /// <summary>A wrong key is SND-CONN-002 on every call.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_wrong_key_is_refused_on_all_three_calls()
    {
        using var inbox = Create();
        inbox.Backing.Seed("me", Sample("o-1"));
        var bad = inbox.WrongKey("me");

        var listed = await inbox.Client.ListOffers(bad, pageToken: null, Token);
        var claimed = await inbox.Client.Claim(bad, "o-1", Token);
        var submitted = await inbox.Client.Submit(bad, "o-1", new InboxResult("c", Succeeded: true, "x", []), Token);

        Expect.Equal(SindriErrors.ConnectionNeedsSignIn.Code, listed.Error.Code);
        Expect.Equal(SindriErrors.ConnectionNeedsSignIn.Code, claimed.Error.Code);
        Expect.Equal(SindriErrors.ConnectionNeedsSignIn.Code, submitted.Error.Code);
    }

    private static Offer Sample(string key) => new(key, "Title " + key, "acme/web", Branch: null, "Do " + key, [], []);
}
