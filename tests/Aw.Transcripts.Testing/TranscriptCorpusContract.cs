using System.Globalization;
using Aw.Transcripts.Core;
using Aw.Transcripts.Core.Ports;
using Aw.Transcripts.Core.Refs;
using Aw.Transcripts.Core.Sessions;
using Aw.Transcripts.Core.Turns;
using Xunit;

namespace Aw.Transcripts.Testing;

/// <summary>
/// The ITranscriptCorpus contract: the same tests run against the in-memory fake and the file corpus
/// (.agents/rules/csharp.md, Seams). A derived class supplies a corpus that holds <see cref="StandardCorpus"/> and an
/// empty one (for the file corpus, over roots that don't exist).
/// </summary>
public abstract class TranscriptCorpusContract
{
    private static readonly CancellationToken _none = CancellationToken.None;
    private static readonly DateTimeOffset _dayBefore = StandardCorpus.Start.AddDays(-1);

    /// <summary>A corpus that holds the standard corpus, and the ref it gives each standard session by name.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The corpus and its refs.</returns>
    protected abstract ValueTask<(ITranscriptCorpus Corpus, Func<string, SessionRef> RefOf)> CreateStandard(CancellationToken cancellationToken);

    /// <summary>A corpus with no transcripts.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The corpus.</returns>
    protected abstract ValueTask<ITranscriptCorpus> CreateEmpty(CancellationToken cancellationToken);

    /// <summary>A list holds the sessions written since the time, newest first, with their counts of typed prompts.</summary>
    [Fact]
    public async Task List_holds_the_sessions_written_since_the_time_newest_first()
    {
        var (corpus, refOf) = await CreateStandard(_none);

        var listed = (await corpus.ListSessions(new SessionQuery(_dayBefore), _none)).Value;

        var expected = StandardCorpus.Transcripts(refOf).Select(t => t.Session).Where(s => s.LastWrittenAt >= _dayBefore).ToList();
        Expect.Same(expected, listed, "a list holds the sessions written since the time, newest first");
    }

    /// <summary>An earlier time also lists the older session.</summary>
    [Fact]
    public async Task An_earlier_time_lists_the_older_session_too()
    {
        var (corpus, refOf) = await CreateStandard(_none);

        var listed = (await corpus.ListSessions(new SessionQuery(StandardCorpus.Start.AddDays(-30)), _none)).Value;

        Expect.Same(StandardCorpus.Names.Select(refOf), listed.Select(s => s.Ref), "an earlier time lists the older session too");
    }

    /// <summary>A subagent names its parent, and a list without subagents leaves it out.</summary>
    [Fact]
    public async Task A_subagent_names_its_parent_and_can_be_left_out()
    {
        var (corpus, refOf) = await CreateStandard(_none);

        var all = (await corpus.ListSessions(new SessionQuery(_dayBefore), _none)).Value;
        var mains = (await corpus.ListSessions(new SessionQuery(_dayBefore) { IncludeSubagents = false }, _none)).Value;

        var subs = all.Where(s => s.IsSubagent).ToList();
        Expect.That(subs.Count == 1 && subs[0].Parent == refOf("main") && subs[0].HumanTurns == 0, "the one subagent names main as its parent and has no human turn");
        Expect.That(mains.Count == 2 && !mains.Any(s => s.IsSubagent), "a list without subagents leaves the subagent out");
    }

    /// <summary>The provider and repo filters keep only matching sessions.</summary>
    [Fact]
    public async Task Provider_and_repo_filters_keep_only_matching_sessions()
    {
        var (corpus, _) = await CreateStandard(_none);

        var codex = (await corpus.ListSessions(new SessionQuery(_dayBefore) { Provider = TranscriptProvider.Codex }, _none)).Value;
        var alpha = (await corpus.ListSessions(new SessionQuery(_dayBefore) { Repo = StandardCorpus.Repo }, _none)).Value;
        var other = (await corpus.ListSessions(new SessionQuery(_dayBefore) { Repo = "/work/other" }, _none)).Value;

        Expect.That(codex.Count == 0, "a provider filter keeps only that provider's sessions");
        Expect.That(alpha.Count == 3 && other.Count == 0, "a repo filter keeps only that repo's sessions");
    }

    /// <summary>A session reads as its turns in order: prompts, and assistant turns with their text and tool names.</summary>
    [Fact]
    public async Task A_session_reads_as_its_turns_in_order()
    {
        var (corpus, refOf) = await CreateStandard(_none);

        var read = (await corpus.ReadSession(refOf("main"), _none)).Value;

        var expected = StandardCorpus.Transcripts(refOf).Single(t => t.Session.Ref == refOf("main"));
        Expect.That(expected.Session == read.Session, $"the session reads as listed: {read.Session}");
        Expect.Same(Shown(expected.Turns), Shown(read.Turns), "a session reads as its turns in order");
    }

    /// <summary>A resumed session reads without the exchange it copied: that prompt appears once in the corpus.</summary>
    [Fact]
    public async Task A_resumed_session_reads_without_the_turns_it_copied()
    {
        var (corpus, refOf) = await CreateStandard(_none);

        var fork = (await corpus.ReadSession(refOf("fork"), _none)).Value;

        Expect.Same(["Human: ship it", "Assistant: Shipped. [Bash]"], fork.Turns.Select(t => $"{t.Role}: {t.Text}{(t.Tools.Count > 0 ? $" [{string.Join(',', t.Tools)}]" : "")}"), "a resumed session reads without the turns it copied");
        Expect.That(fork.Session.HumanTurns == 1, "a resumed session counts only its own prompts");
    }

    /// <summary>Refs are the same on every call, and every listed ref reads back as its session.</summary>
    [Fact]
    public async Task Refs_are_stable_and_resolve_back_to_their_session()
    {
        var (corpus, _) = await CreateStandard(_none);

        var first = (await corpus.ListSessions(new SessionQuery(_dayBefore), _none)).Value;
        var second = (await corpus.ListSessions(new SessionQuery(_dayBefore), _none)).Value;

        Expect.Same(first.Select(s => s.Ref), second.Select(s => s.Ref), "refs are the same on every call");
        foreach (var session in first)
        {
            var read = await corpus.ReadSession(session.Ref, _none);
            Expect.That(read.IsSuccess && read.Value.Session == session, $"the ref {session.Ref.Value} reads back as its session");
            Expect.That(read.Value.Turns.All(t => t.Ref.SessionOf() == session.Ref), $"every turn of {session.Ref.Value} names it");
        }
    }

    /// <summary>A ref no file has is AW-TRANSCRIPTS-002.</summary>
    [Fact]
    public async Task An_unknown_ref_is_not_found()
    {
        var (corpus, _) = await CreateStandard(_none);

        var read = await corpus.ReadSession(SessionRef.From("transcript:000000000000"), _none);

        Expect.That(read.IsError && read.Error.Code == TranscriptsErrors.SessionNotFound.Code, "a ref no file has is AW-TRANSCRIPTS-002");
    }

    /// <summary>An empty corpus lists nothing, and that is not an error.</summary>
    [Fact]
    public async Task An_empty_corpus_lists_nothing()
    {
        var corpus = await CreateEmpty(_none);

        var listed = await corpus.ListSessions(new SessionQuery(DateTimeOffset.MinValue), _none);

        Expect.That(listed.IsSuccess && listed.Value.Count == 0, "an empty corpus lists nothing, without an error");
    }

    private static List<string> Shown(IEnumerable<TranscriptTurn> turns) =>
        [.. turns.Select(t => string.Create(CultureInfo.InvariantCulture, $"{t.Ref} {t.Role} {t.At:O} {t.Text} [{string.Join(',', t.Tools)}]"))];
}
