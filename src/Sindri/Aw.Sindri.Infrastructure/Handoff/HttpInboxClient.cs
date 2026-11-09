using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Sindri.Core.Handoff;

namespace Aw.Sindri.Infrastructure.Handoff;

/// <summary>IInboxClient over the three HTTP calls of schemas/handoff/inbox.openapi.yaml (stub: the calls arrive in the next commit).</summary>
/// <param name="http">The HTTP client (it should not follow redirects).</param>
/// <param name="allowedHosts">The hosts an inbox may be on.</param>
/// <param name="retryDelay">How long list and submit wait before a retry.</param>
[SuppressMessage("Meziantou.Analyzer", "MA0182:Internal type is apparently never used", Justification = "The inbox poller (the next change in this series) builds it; until then only the tests do.")]
internal sealed class HttpInboxClient(HttpClient http, IReadOnlyCollection<string> allowedHosts, TimeSpan retryDelay) : IInboxClient
{
    private readonly HttpClient _http = http;
    private readonly IReadOnlyCollection<string> _allowedHosts = allowedHosts;
    private readonly TimeSpan _retryDelay = retryDelay;

    public ValueTask<Result<OfferPage>> ListOffers(InboxTarget target, string? pageToken, CancellationToken cancellationToken) => throw new NotSupportedException(string.Create(CultureInfo.InvariantCulture, $"{_http}{_allowedHosts.Count}{_retryDelay}"));

    public ValueTask<Result<ClaimResult>> Claim(InboxTarget target, string offerKey, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> Submit(InboxTarget target, string offerKey, InboxResult result, CancellationToken cancellationToken) => throw new NotSupportedException();
}
