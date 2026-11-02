using System.Globalization;
using Aw.Context.Testing.Records;

namespace Aw.Context.Tests.Linear;

/// <summary>The Linear project feed keeps the resume contract (#1679, owner rule #1682): twelve projects, five to a pull, a 429 after one pull or after two.</summary>
[Trait("Lane", "unit")]
public sealed class LinearProjectFeedResumeTests : RecordFeedResumeContract
{
    private static readonly DateTimeOffset _at = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    protected override int Limit => 5;

    // why: one GraphQL call per pull.
    protected override int MaxRequestsPerPull => 1;

    protected override int EarlyInterruption => 1;

    protected override int LateInterruption => 2;

    protected override ResumeUpstream Create()
    {
        var ids = Enumerable.Range(1, 12).Select(static n => string.Create(CultureInfo.InvariantCulture, $"p{n:00}")).ToList();
        var fake = new FakeLinearProjects([.. ids.Select((id, n) => FakeLinearProjects.Node(id, _at.AddMinutes(n)))]);
        return new LinearResumeUpstream(fake, ids.Select(static id => "linear:project/" + id), static (sources, settings) => sources.CreateProjectFeed(settings));
    }
}
