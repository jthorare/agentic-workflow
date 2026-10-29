using Aw.Context.Testing.Records;

namespace Aw.Sindri.Tests.IndexSources;

/// <summary>The Slack connector, through the feed the sync run reads, keeps the resume contract (#1679, owner rule #1682).</summary>
[Trait("Lane", "unit")]
public sealed class SlackFeedResumeTests : RecordFeedResumeContract
{
    // why: the source holds 33 records (3 day records, 30 threads), so a limit of 10 makes it several pulls; the day records count toward the limit.
    protected override int Limit => 10;

    // why: one history page, then a thread read per record until the limit of 10 is met, with room for a second history page.
    protected override int MaxRequestsPerPull => 12;

    // why: the early point lands inside the first pull's thread reads (history, then two threads); the late one lands inside the second pull (the first makes 8 requests, the second starts at 9).
    protected override int EarlyInterruption => 3;

    protected override int LateInterruption => 14;

    protected override ResumeUpstream Create() => new SlackResumeUpstream();

    // why: a day record is rebuilt from its first message when a pull resumes inside that day, so a day may come again.
    protected override bool MayRepeatKey(string key) => key.Contains("/day/", StringComparison.Ordinal);

    // why: a resumed pull reads its chunk's history again to find its place; the thread reads are what must not repeat.
    protected override bool MayRepeatRequest(string request) => request.StartsWith("history ", StringComparison.Ordinal);
}
