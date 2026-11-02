using System.Globalization;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Linear;

/// <summary>The Linear issue feed keeps the resume contract (#1679, owner rule #1682): twelve issues, five to a pull, a 429 after one pull or after two.</summary>
[Trait("Lane", "unit")]
public sealed class LinearFeedResumeTests : RecordFeedResumeContract
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    protected override int Limit => 5;

    // why: one GraphQL call per pull.
    protected override int MaxRequestsPerPull => 1;

    protected override int EarlyInterruption => 1;

    protected override int LateInterruption => 2;

    protected override ResumeUpstream Create()
    {
        var ids = Enumerable.Range(1, 12).Select(static n => string.Create(CultureInfo.InvariantCulture, $"i{n:00}")).ToList();
        var fake = new FakeLinearIssues([.. ids.Select((id, n) => FakeLinearIssues.Node(id, _at.AddMinutes(n)))]);
        return new LinearResumeUpstream(fake, ids.Select(static id => "linear:issue/" + id), static (sources, settings) => sources.CreateFeed(settings));
    }
}
