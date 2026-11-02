using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Context.Infrastructure.Linear;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.Linear;

/// <summary>One linear source over a fake Linear server, built the way the registered factory builds it (LinearSourceContractTests binds ContextSourceContract to it).</summary>
internal sealed class LinearSourceHarness : IDisposable
{
    public const string Pointer = "env:LINEAR_FIXTURE_KEY";
    public static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The deadline every call gets unless a test exercises the deadline itself.</summary>
    public static readonly TimeSpan Patient = TimeSpan.FromSeconds(30);

    /// <summary>A harness whose pointer resolves to <paramref name="token"/>.</summary>
    /// <param name="url">The fake server's GraphQL endpoint.</param>
    /// <param name="token">What the pointer resolves to (null: nothing).</param>
    /// <param name="teams">The profile's Linear teams.</param>
    public LinearSourceHarness(Uri url, string? token, params string[] teams)
    {
        Settings = LinearSettings.Create(url.ToString(), Pointer, teams.Length == 0 ? ["SPL"] : teams).Value;
        Sources = new LinearSources(new LinearSourceParts(Http, name => string.Equals(name, Pointer, StringComparison.Ordinal) ? token : null, new FakeTimeProvider(Now), Log) { CallDeadline = Patient });
        Source = Sources.Create(Settings);
    }

    public LinearSources Sources { get; }

    public LinearSettings Settings { get; }

    public IContextSource Source { get; }

    public CapturingLogger<LinearClient> Log { get; } = new();

    public HttpClient Http { get; } = new(new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });

    public static SourceQuery Query(params string[] terms) => new(terms, AsOf: null, Limit: 20, Patient);

    public void Dispose() => Http.Dispose();
}
