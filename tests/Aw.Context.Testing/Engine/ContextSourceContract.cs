using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Xunit;

namespace Aw.Context.Testing.Engine;

/// <summary>
/// The contract every <see cref="IContextSource"/> honours (spec §10), run against the fake and every real adapter
/// alike. A derived test class binds <see cref="Create"/> to one implementation over the split-panes fixture's docs
/// (which hold 'orientation' and 'layout') and carries its lane.
/// </summary>
/// <typeparam name="TSource">The implementation.</typeparam>
public abstract class ContextSourceContract<TSource>
    where TSource : IContextSource
{
    private static readonly SourceQuery _orientation = new(["orientation"], AsOf: null, Limit: 50, TimeSpan.FromSeconds(30));   // only bounds a hang: a real source on a loaded machine must not miss it

    /// <summary>A fresh source over the fixture's docs.</summary>
    /// <returns>The source.</returns>
    protected abstract Task<TSource> Create();

    /// <summary>
    /// False for a live source with no route to a record's version by id (spec §5.2). Its Versions answers
    /// Unavailable, so its citations stay 'version unchecked', and its Fetch reads the record as it is now.
    /// </summary>
    protected virtual bool ChecksVersions => true;

    /// <summary>Hits are ranked 1, 2, … in order, at most the limit, each carrying a canonical key and a version.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Search_ranks_from_one_within_the_limit()
    {
        var source = await Create();

        var hits = Expect(await source.Search(_orientation with { Terms = ["layout", "orientation"], Limit = 1 }, CancellationToken.None));
        var all = Expect(await source.Search(_orientation, CancellationToken.None));

        Check(hits.Count == 1, "a limit of 1 returns one hit");
        Check(all.Select(h => h.Rank).SequenceEqual(Enumerable.Range(1, all.Count)), "ranks run 1, 2, … in order");
        Check(all.All(h => h.Ref.Source == source.Describe().Id), "every hit names this source");
    }

    /// <summary>
    /// The descriptor states where the source runs and any limits it declares (README X61): a placement from the enum,
    /// limits, when declared, well formed, and none on a local source (CTX D6).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Describe_states_placement_and_well_formed_limits()
    {
        var described = (await Create()).Describe();

        Check(Enum.IsDefined(described.Placement), "the placement is Local or Shared");
        Check(described.Limits is null || described.Limits.IsWellFormed, "declared limits are positive, a request count with its window");
        Check(described.Placement != SourcePlacement.Local || described.Limits is null, "a local source declares no limits (CTX D6, README X61)");
    }

    /// <summary>A term the docs don't hold finds nothing (and is never read as query syntax).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Search_without_a_match_is_empty_even_for_syntax()
    {
        var source = await Create();

        var none = Expect(await source.Search(_orientation with { Terms = ["zzqx", "\" OR 1 OR \"", "NEAR(", "*"] }, CancellationToken.None));

        Check(none.Count == 0, "no hit for terms the docs don't hold");
    }

    /// <summary>Fetching a hit returns its record at the same reference, with text.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Fetch_returns_the_hit_s_record()
    {
        var source = await Create();
        var hit = Expect(await source.Search(_orientation, CancellationToken.None))[0];

        var record = Expect(await source.Fetch(hit.Ref, CancellationToken.None));

        Check(record.Ref == hit.Ref, "the record carries the hit's reference");
        Check(record.Text.Contains("rientation", StringComparison.Ordinal), "its text holds the term");
    }

    /// <summary>A version that doesn't exist is NotFound, not an exception (or, for a source that can't check versions, the record now).</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Fetch_of_a_missing_version_is_not_found()
    {
        var source = await Create();
        var hit = Expect(await source.Search(_orientation, CancellationToken.None))[0];

        var missing = await source.Fetch(hit.Ref with { Version = SourceVersion.From("0000000000000000000000000000000000000000") }, CancellationToken.None);

        Check(ChecksVersions ? missing.IsError && missing.Error.Kind == ErrorKind.NotFound : missing.IsSuccess && missing.Value.Ref.Id == hit.Ref.Id,
            ChecksVersions ? "a missing version is NotFound" : "a source that can't check versions fetches the record as it is now");
    }

    /// <summary>
    /// Versions answers one entry per reference, in order, and an unchanged record is at its cited version (or, for a
    /// source that can't check versions, Unavailable).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Versions_are_batched_and_in_order()
    {
        var source = await Create();
        var hits = Expect(await source.Search(_orientation with { Terms = ["layout", "orientation"] }, CancellationToken.None));

        var answer = await source.Versions([.. hits.Select(h => h.Ref)], CancellationToken.None);
        if (!ChecksVersions)
        {
            Check(answer.IsError && answer.Error.Kind == ErrorKind.Unavailable, "a source that can't check versions answers Unavailable");
            return;
        }

        var versions = Expect(answer);
        Check(versions.Count == hits.Count, "one version per reference");
        Check(hits.Select(h => (SourceVersion?)h.Ref.Version).SequenceEqual(versions), "unchanged records are at their cited versions");
    }

    /// <summary>A healthy source probes ok.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Probe_is_ok()
    {
        var source = await Create();

        Check((await source.Probe(CancellationToken.None)).State == SourceState.Ok, "a healthy source probes ok");
    }

    private static T Expect<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new ContractViolationException($"{typeof(TSource).Name}: {result.Error.Code} {result.Error.Message}");

    private static void Check(bool holds, string what)
    {
        if (!holds)
        {
            throw new ContractViolationException($"{typeof(TSource).Name}: {what}");
        }
    }
}
