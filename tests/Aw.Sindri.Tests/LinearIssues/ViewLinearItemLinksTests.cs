using Aw.Kernel;
using Aw.Context.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.LinearIssues;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.LinearIssues;

/// <summary>The Linear hub's item and PR chips come from the 'item-links' view (Sindri v2 PR 4b), not from a workflow instance's facts.</summary>
public sealed class ViewLinearItemLinksTests
{
    private const string Record = "linear:issue/abc-123";
    private const string PrUrl = "https://forge.example/acme/widgets/pull/12";
    private readonly FakeReviewFacts _pulls = new();
    private readonly Rows _rows = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task PR_chips_come_from_item_links_not_line_facts()
    {
        _ = _rows.WithItem(A.Item().Build() with { Link = RecordKey.From(Record) }).WithLink(PrLink());
        _ = _pulls.With(new ReviewFactsSnapshot([Pull()], Github: null, new ReviewLibraryFacts(Reading: false, Docs: 0, WithErrors: 0, UnreadableRoots: [])));

        var links = (await Subject().For([Record], Token)).Value;

        var pr = links[Record].Pr;
        Assert.Equal((12, PrUrl, "open"), (pr?.Number ?? 0, pr?.Url, pr?.Review));
        Assert.Equal(A.FirstItemId.Value, links[Record].Link?.Item);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_PR_chip_reads_unknown_when_the_stored_PRs_cant_be_read()
    {
        _ = _rows.WithItem(A.Item().Build() with { Link = RecordKey.From(Record) }).WithLink(PrLink());
        _ = _pulls.Failing(ContextErrors.SessionsDisabled.With("github failed"));

        var links = (await Subject().For([Record], Token)).Value;

        Assert.Equal("unknown", links[Record].Pr?.Review);
    }

    private static ItemLinkRow PrLink() => new(A.FirstItemId, ItemLinkKind.Pr, "acme/widgets#12", PrUrl, "open", DateTimeOffset.UnixEpoch, Sequence: 2);

    private static ReviewPull Pull() => new("acme/widgets", 12, "Add a retry budget", "sam-dev", Draft: false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, HeadSha: null, [], [], ReviewersRead: true, [], PrUrl);

    private ViewLinearItemLinks Subject() => new(_rows, _pulls, new NoProfile());

    private sealed class NoProfile : IApprovedProfile
    {
        public ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken) => ValueTask.FromResult(Results.Success<LoadedProfile?>(value: null));
    }

    private sealed class Rows : IViewReader
    {
        private readonly List<ItemRow> _items = [];
        private readonly List<ItemLinkRow> _links = [];

        public Rows WithItem(ItemRow row)
        {
            _items.Add(row);
            return this;
        }

        public Rows WithLink(ItemLinkRow row)
        {
            _links.Add(row);
            return this;
        }

        public ValueTask<Result<IReadOnlyList<TRow>>> List<TRow>(string view, CancellationToken cancellationToken)
            where TRow : class =>
            ValueTask.FromResult(Results.Success<IReadOnlyList<TRow>>(view switch
            {
                ViewNames.Items => [.. _items.Cast<TRow>()],
                ViewNames.ItemLinks => [.. _links.Cast<TRow>()],
                _ => [],
            }));

        public ValueTask<Result<TRow?>> Get<TRow>(string view, string key, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<TRow>>> Range<TRow>(string view, string afterKey, string? beforeKey, int max, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<IReadOnlyList<TRow>>> Where<TRow>(string view, ViewColumn column, string value, CancellationToken cancellationToken)
            where TRow : class => throw new NotSupportedException();

        public ValueTask<Result<ProjectorState?>> State(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
