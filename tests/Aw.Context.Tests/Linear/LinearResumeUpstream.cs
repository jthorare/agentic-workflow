using System.Net;
using System.Text.Json.Nodes;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sync;
using Aw.Context.Infrastructure.Linear;
using Aw.Context.Testing.Records;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// A Linear that holds a list, a fake GraphQL handler (<see cref="FakeLinearIssues"/> or <see cref="FakeLinearProjects"/>) behind the real <see cref="LinearClient"/> and the real feed. Once <see cref="ResumeUpstream.InterruptAfter"/> has run out it answers
/// HTTP 429, as Linear's rate limit does. A request is logged as its cursor and filter.
/// </summary>
internal sealed class LinearResumeUpstream : ResumeUpstream
{
    private readonly List<string> _keys;

    public LinearResumeUpstream(HttpMessageHandler fake, IEnumerable<string> keys, Func<LinearSources, LinearSettings, RecordFeed> feed)
    {
        _keys = [.. keys];
        var settings = LinearSettings.Create("http://127.0.0.1:1/graphql", "env:LINEAR_FIXTURE_KEY", ["SPL"]).Value;
        var parts = new LinearSourceParts(new HttpClient(new Gate(this, fake)), static _ => "fixture-key", new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 9, 0, 0, TimeSpan.Zero)), new CapturingLogger<LinearClient>());
        Feed = feed(new LinearSources(parts), settings);
    }

    public override RecordFeed Feed { get; }

    public override IReadOnlyCollection<string> Keys => _keys;

    private sealed class Gate(LinearResumeUpstream owner, HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var variables = body?["variables"];
            var name = "after=" + variables?["after"]?.ToJsonString() + " filter=" + variables?["filter"]?.ToJsonString();
            return owner.Admit(name)
                ? await base.SendAsync(request, cancellationToken).ConfigureAwait(false)
                : new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        }
    }
}
