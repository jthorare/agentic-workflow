using Aw.Context.Core.Docs;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Trust;
using Aw.Context.Testing.Fixtures;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IDocIndex"/> honours, run against the fake and the SQLite index over the fixture
/// repo (its two docs under 'docs/'). A binding creates the index (and whatever store it needs) and carries its lane.
/// </summary>
/// <typeparam name="TIndex">The implementation.</typeparam>
public abstract class DocIndexContract<TIndex> : IDisposable
    where TIndex : IDocIndex
{
    private readonly FixtureRepo _repo = FixtureRepo.Build();

    /// <summary>The fixture repo as a profile repo.</summary>
    protected ContextRepo Repo => new(ContextRepoName.From("split-panes"), _repo.Path);

    /// <summary>A fresh index with nothing in it.</summary>
    /// <returns>The index.</returns>
    protected abstract TIndex Create();

    /// <summary>The scrubbers for <paramref name="patterns"/> (a binding over real storage passes the real factory's).</summary>
    /// <param name="patterns">The extra patterns.</param>
    /// <returns>The scrubbers.</returns>
    protected virtual Scrubbers ScrubbersFor(IReadOnlyList<ScrubPattern> patterns) => new FakeScrubberFactory().For(patterns).Value;

    private Scrubbers Plain => ScrubbersFor([]);

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Before any refresh a repo has no head and no sync time.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_repo_never_refreshed_has_no_sync()
    {
        var status = Expect(await Create().Status(Repo, Plain.Ruleset, CancellationToken.None));

        Check(status is { Head: null, LastSync: null, Documents: 0 }, "a repo never refreshed has nothing");
    }

    /// <summary>A refresh indexes the docs the globs match; a second one keeps them and adds none.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_refresh_indexes_and_a_second_keeps()
    {
        var index = Create();

        var first = Expect(await index.Refresh(Repo, ["docs/**/*.md"], Plain, CancellationToken.None));
        var second = Expect(await index.Refresh(Repo, ["docs/**/*.md"], Plain, CancellationToken.None));
        var status = Expect(await index.Status(Repo, Plain.Ruleset, CancellationToken.None));

        Check(first is { Added: 2, Code: null } && first.Head is not null, $"the first refresh adds the fixture's 2 docs (added {first.Added.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        Check(second is { Added: 0, Kept: 2 }, "the second refresh keeps them");
        Check(status.LastSync is not null && status.Documents == 2 && status.Chunks >= 2 && status.Code is null, "the status reflects the refresh");
    }

    /// <summary>The globs choose the documents: a glob that matches one of the two indexes one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Only_the_documents_the_globs_match_are_indexed()
    {
        var refreshed = Expect(await Create().Refresh(Repo, ["docs/decisions/*.md"], Plain, CancellationToken.None));

        Check(refreshed is { Added: 1, Kept: 0, Removed: 0 }, $"one document matches the glob (added {refreshed.Added.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
    }

    /// <summary>A document deleted in a new commit is removed; an edited one is indexed again, not kept.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_deleted_document_is_removed_and_an_edited_one_added_again()
    {
        var index = Create();
        _ = Expect(await index.Refresh(Repo, ["docs/**/*.md"], Plain, CancellationToken.None));
        File.Delete(Path.Combine(_repo.Path, "docs", "architecture.md"));
        await File.AppendAllTextAsync(Path.Combine(_repo.Path, "docs", "decisions", "0001-orientation-toggle.md"), "\nAn edit.\n", CancellationToken.None);
        Commit("edit and delete");

        var refreshed = Expect(await index.Refresh(Repo, ["docs/**/*.md"], Plain, CancellationToken.None));
        var status = Expect(await index.Status(Repo, Plain.Ruleset, CancellationToken.None));

        Check(refreshed is { Added: 1, Removed: 1, Kept: 0 }, "the deleted doc is removed and the edited one added again");
        Check(status.Documents == 1, "one document is left");
    }

    /// <summary>
    /// A document stored under another scrub ruleset is outdated until the next refresh, which cleans it again with the
    /// new rules even though its blob didn't change; a refresh under the same rules keeps it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_document_stored_under_another_ruleset_is_cleaned_again()
    {
        var index = Create();
        var acme = ScrubbersFor([new ScrubPattern("acme-customer-id", "ACME-[0-9]{6}")]);
        _ = Expect(await index.Refresh(Repo, ["docs/**/*.md"], Plain, CancellationToken.None));

        var outdated = Expect(await index.Status(Repo, acme.Ruleset, CancellationToken.None));
        var recleaned = Expect(await index.Refresh(Repo, ["docs/**/*.md"], acme, CancellationToken.None));
        var kept = Expect(await index.Refresh(Repo, ["docs/**/*.md"], acme, CancellationToken.None));
        var current = Expect(await index.Status(Repo, acme.Ruleset, CancellationToken.None));

        Check(outdated.Outdated == 2, $"both documents are outdated under the new rules (got {outdated.Outdated.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        Check(recleaned is { Added: 2, Kept: 0, Removed: 0, Recleaned: 2 }, $"the refresh cleans both again (added {recleaned.Added.ToString(System.Globalization.CultureInfo.InvariantCulture)}, recleaned {recleaned.Recleaned.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        Check(kept is { Added: 0, Kept: 2, Recleaned: 0 } && current.Outdated == 0, "a refresh under the same rules keeps them");
    }

    /// <summary>Read answers each indexed document's path, blob and scrubbed text in path order, and nothing before a refresh.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Read_answers_the_indexed_documents_with_their_text()
    {
        var index = Create();
        var before = Expect(await index.Read(Repo, CancellationToken.None));
        _ = Expect(await index.Refresh(Repo, ["docs/**/*.md"], Plain, CancellationToken.None));

        var docs = Expect(await index.Read(Repo, CancellationToken.None));

        Check(before.Count == 0, "a repo never refreshed has no documents");
        Check(docs.Select(d => d.Path).SequenceEqual(["docs/architecture.md", "docs/decisions/0001-orientation-toggle.md"], StringComparer.Ordinal), "the two fixture docs, in path order");
        Check(docs.All(d => d.Blob.Length > 0 && !string.IsNullOrWhiteSpace(d.Text)), "each has a blob and its text");
    }

    /// <summary>Deletes the fixture repo.</summary>
    /// <param name="disposing">True from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repo.Dispose();
        }
    }

    private void Commit(string message)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in (string[])["-C", _repo.Path, "-c", "user.name=fixture", "-c", "user.email=fixture@example.invalid", "-c", "commit.gpgsign=false", "commit", "-q", "-am", message])
        {
            start.ArgumentList.Add(arg);
        }

        using var git = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
        git.WaitForExit();
        Check(git.ExitCode == 0, "the fixture commit worked");
    }

    private static T Expect<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{typeof(TIndex).Name}: {result.Error.Code} {result.Error.Message}");

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TIndex).Name}: {what}");
        }
    }
}
