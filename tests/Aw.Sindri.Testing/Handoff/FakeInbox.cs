using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Handoff;

namespace Aw.Sindri.Testing.Handoff;

/// <summary>
/// An in-memory handoff inbox: the reference an implementer reads next to schemas/handoff/inbox.openapi.yaml, and the state behind <see cref="FakeInboxServer"/>.
/// It accepts one bearer key, lists an owner's unclaimed offers in seed order in pages (the page token is the last offer key returned), lets exactly one
/// claim win, and takes a result once per claim however often it is sent.
/// </summary>
/// <param name="key">The bearer key it accepts.</param>
/// <param name="pageSize">How many offers one list page holds.</param>
public sealed class FakeInbox(string key, int pageSize = 50) : IInboxClient
{
    private readonly Lock _gate = new();
    private readonly List<Entry> _entries = [];
    private readonly List<(string OfferKey, InboxResult Result)> _submissions = [];
    private int _claims;

    /// <summary>Gets the key it accepts.</summary>
    public string Key { get; } = key;

    /// <summary>Gets how many offers one list page holds.</summary>
    public int PageSize { get; } = pageSize;

    /// <summary>Gets the results it took, in order: the offer and the result.</summary>
    public IReadOnlyList<(string OfferKey, InboxResult Result)> Submissions
    {
        get
        {
            lock (_gate)
            {
                return [.. _submissions];
            }
        }
    }

    /// <summary>Adds an unclaimed offer for an owner.</summary>
    /// <param name="owner">The owner it is offered to.</param>
    /// <param name="offer">The offer.</param>
    public void Seed(string owner, Offer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        lock (_gate)
        {
            _entries.Add(new Entry(owner, offer));
        }
    }

    /// <summary>Makes an offer expire: a claim of it is then gone.</summary>
    /// <param name="offerKey">The offer.</param>
    public void Expire(string offerKey)
    {
        lock (_gate)
        {
            foreach (var entry in _entries.Where(e => e.Offer.Key == offerKey))
            {
                entry.Expired = true;
            }
        }
    }

    /// <summary>Says whether an offer was made to expire.</summary>
    /// <param name="offerKey">The offer.</param>
    /// <returns>True when <see cref="Expire"/> was called for it.</returns>
    public bool IsExpired(string offerKey)
    {
        lock (_gate)
        {
            return _entries.Exists(e => e.Offer.Key == offerKey && e.Expired);
        }
    }

    /// <summary>Finds the owner an offer was seeded for.</summary>
    /// <param name="offerKey">The offer.</param>
    /// <returns>The owner, or null for an offer it never had.</returns>
    public string? OwnerOf(string offerKey)
    {
        lock (_gate)
        {
            return _entries.Find(e => e.Offer.Key == offerKey)?.Owner;
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<OfferPage>> ListOffers(InboxTarget target, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Accepts(target))
        {
            return ValueTask.FromResult<Result<OfferPage>>(Refused());
        }

        lock (_gate)
        {
            var open = _entries.Where(e => e.Owner == target.Owner && e.ClaimKey is null && !e.Expired).Select(static e => e.Offer).ToList();
            var start = 0;
            if (pageToken is not null)
            {
                var after = pageToken.StartsWith("after:", StringComparison.Ordinal) ? pageToken["after:".Length..] : null;
                var at = after is null ? -1 : _entries.FindIndex(e => e.Offer.Key == after);
                if (at < 0)
                {
                    return ValueTask.FromResult<Result<OfferPage>>(SindriErrors.InboxContractViolation.With("pageToken is not one this inbox gave"));
                }

                start = open.FindIndex(o => _entries.FindIndex(e => e.Offer.Key == o.Key) > at);
                start = start < 0 ? open.Count : start;
            }

            var page = open.Skip(start).Take(PageSize).ToList();
            var more = start + page.Count < open.Count;
            return ValueTask.FromResult<Result<OfferPage>>(new OfferPage(page, more ? "after:" + page[^1].Key : null));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<ClaimResult>> Claim(InboxTarget target, string offerKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Accepts(target))
        {
            return ValueTask.FromResult<Result<ClaimResult>>(Refused());
        }

        lock (_gate)
        {
            var entry = _entries.Find(e => e.Offer.Key == offerKey && e.Owner == target.Owner);
            if (entry is null || entry.Expired)
            {
                return ValueTask.FromResult<Result<ClaimResult>>(SindriErrors.InboxOfferGone.With("the inbox has no such offer"));
            }

            if (entry.ClaimKey is not null)
            {
                return ValueTask.FromResult<Result<ClaimResult>>(SindriErrors.InboxClaimLost.With("another caller claimed the offer first"));
            }

            _claims++;
            entry.ClaimKey = "c-" + _claims.ToString(CultureInfo.InvariantCulture);
            return ValueTask.FromResult<Result<ClaimResult>>(new ClaimResult(entry.ClaimKey, entry.Offer));
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Submit(InboxTarget target, string offerKey, InboxResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(result);
        if (!Accepts(target))
        {
            return ValueTask.FromResult<Result<Unit>>(Refused());
        }

        lock (_gate)
        {
            var entry = _entries.Find(e => e.Offer.Key == offerKey && e.Owner == target.Owner);
            if (entry is null)
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.InboxOfferGone.With("the inbox has no such offer"));
            }

            if (entry.ClaimKey != result.ClaimKey)
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.InboxSubmitFailed.With("the claim is not the one this offer's claim returned"));
            }

            if (!_submissions.Exists(s => s.Result.ClaimKey == result.ClaimKey))
            {
                _submissions.Add((offerKey, result));
            }

            return ValueTask.FromResult(Results.Ok());
        }
    }

    private bool Accepts(InboxTarget target) => target.Credential.AccessToken == Key;

    private static Error Refused() => SindriErrors.ConnectionNeedsSignIn.With("the inbox refused the key");

    private sealed class Entry(string owner, Offer offer)
    {
        public string Owner { get; } = owner;

        public Offer Offer { get; } = offer;

        public string? ClaimKey { get; set; }

        public bool Expired { get; set; }
    }
}
