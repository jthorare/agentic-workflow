using Aw.Kernel;

namespace Aw.Sindri.Core.Handoff;

/// <summary>
/// The three calls of the handoff inbox contract (schemas/handoff/inbox.openapi.yaml, docs/sindri/handoff.md). A real client retries list and submit,
/// never claim: a claim that timed out may have won, and a second claim would then lose to the first.
/// </summary>
public interface IInboxClient
{
    /// <summary>Lists the unclaimed offers for the target's owner.</summary>
    /// <param name="target">The inbox.</param>
    /// <param name="pageToken">The token a page returned, or null for the first page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A page, SND-CONN-002 when the key is refused, SND-CONN-201 when the answer breaks the contract, SND-CONN-205 when the inbox is out of reach or SND-CONN-206 when its host is not allowed.</returns>
    public ValueTask<Result<OfferPage>> ListOffers(InboxTarget target, string? pageToken, CancellationToken cancellationToken);

    /// <summary>Claims one offer. Exactly one caller wins.</summary>
    /// <param name="target">The inbox.</param>
    /// <param name="offerKey">The offer's id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The claim, SND-CONN-202 when another caller won (informational: nothing starts), SND-CONN-203 when the offer is gone, or the errors of <see cref="ListOffers"/>.</returns>
    public ValueTask<Result<ClaimResult>> Claim(InboxTarget target, string offerKey, CancellationToken cancellationToken);

    /// <summary>Submits a claimed offer's result. A repeat with the same claim is accepted and changes nothing.</summary>
    /// <param name="target">The inbox.</param>
    /// <param name="offerKey">The offer's id.</param>
    /// <param name="result">The result.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, SND-CONN-204 when the inbox refuses the result, or the errors of <see cref="ListOffers"/>.</returns>
    public ValueTask<Result<Unit>> Submit(InboxTarget target, string offerKey, InboxResult result, CancellationToken cancellationToken);
}
