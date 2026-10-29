using Aw.Context.Testing.Records;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>The Slack connector, through the feed the sync run reads, keeps the resume contract (#1679, owner rule #1682).</summary>
[Trait("Lane", "unit")]
public sealed class SlackFeedResumeTests : RecordFeedResumeContract
{
    protected override int Limit => 100;

    // why: one history page, then at most 20 thread reads (PullSlackService.RepliesPerPull), with room for a second history page.
    protected override int MaxRequestsPerPull => 25;

    // why: the early point lands inside the first pull's thread reads (history, then two threads); the late one lands inside the second pull.
    protected override int EarlyInterruption => 3;

    protected override int LateInterruption => 25;

    protected override ResumeUpstream Create() => new SlackResumeUpstream();

    // why: a day record is rebuilt from its first message when a pull resumes inside that day, so a day may come again.
    protected override bool MayRepeatKey(string key) => key.Contains("/day/", StringComparison.Ordinal);

    // why: a resumed pull reads its chunk's history again to find its place; the thread reads are what must not repeat.
    protected override bool MayRepeatRequest(string request) => request.StartsWith("history ", StringComparison.Ordinal);
}
