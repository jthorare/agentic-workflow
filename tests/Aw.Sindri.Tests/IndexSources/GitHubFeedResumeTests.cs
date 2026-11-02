using Aw.Context.Testing.Records;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>The GitHub connector, through the feed the sync run reads, keeps the resume contract (#1679, owner rule #1682).</summary>
[Trait("Lane", "unit")]
public sealed class GitHubFeedResumeTests : RecordFeedResumeContract
{
    protected override int Limit => 4;

    // why: one list read and one pull request read per entry of a four-entry page.
    protected override int MaxRequestsPerPull => 5;

    // why: the early point lands inside the first page's pull request reads, the late one inside the second page's.
    protected override int EarlyInterruption => 3;

    protected override int LateInterruption => 8;

    protected override ResumeUpstream Create() => new GitHubResumeUpstream();
}
