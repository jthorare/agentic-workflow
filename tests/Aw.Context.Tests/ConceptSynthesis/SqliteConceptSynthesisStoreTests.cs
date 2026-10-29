using System.Globalization;
using Aw.Context.Core.ConceptSynthesis;
using Aw.Context.Infrastructure.ConceptSynthesis;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Tests.Map;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Context.Tests.ConceptSynthesis;

[Trait("Lane", "integration")]
public sealed class SqliteConceptSynthesisStoreTests : IDisposable
{
    private static readonly DateTimeOffset _t0 = DateTimeOffset.Parse("2026-10-29T09:00:00Z", CultureInfo.InvariantCulture);
    private static readonly DateTimeOffset _t1 = _t0.AddHours(2);

    private readonly TempHome _home = new();
    private readonly ContextDatabase _database;
    private readonly SqliteConceptSynthesisStore _store;

    public SqliteConceptSynthesisStoreTests()
    {
        _database = new ContextDatabase(SettingsFromEnvironment.Read(_home.Environment), new FakeTimeProvider());
        _store = new SqliteConceptSynthesisStore(_database);
    }

    public void Dispose()
    {
        _database.Dispose();
        _home.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SynthesizedConcept Concept(string key, string name = "Checkout retry", string summary = "Retries a failed payment.", params ConceptMember[] members) =>
        new(key, name, summary, members.Length == 0 ? [new ConceptMember("rec-1", "3", "linear")] : members, "hash-" + key, SynthesisState.Done, 42, _t0, _t0);

    [Fact]
    public async Task All_is_empty_when_the_database_has_no_table_yet()
    {
        var all = await _store.All(Ct);

        Assert.False(all.IsError);
        Assert.Empty(all.Value);
    }

    [Fact]
    public async Task Save_then_All_round_trips_every_field()
    {
        var saved = new SynthesizedConcept("checkout-retry", "Checkout retry", "Retries a failed payment.",
            [new ConceptMember("rec-1", "3", "linear"), new ConceptMember("rec-2", "abc", "github")], "h1", SynthesisState.Queued, 42, _t0, _t0);

        Assert.False((await _store.Save(saved, Ct)).IsError);
        var all = await _store.All(Ct);

        var read = Assert.Single(all.Value);
        Assert.Equal(saved with { Members = [] }, read with { Members = [] });
        Assert.Equal(saved.Members, read.Members);
    }

    [Fact]
    public async Task Saving_an_existing_key_updates_members_and_summary_and_keeps_created_at()
    {
        _ = await _store.Save(Concept("checkout-retry"), Ct);
        var relabeled = Concept("checkout-retry", "Payment retries", "New summary.", new ConceptMember("rec-9", "1", "slack")) with { MembershipHash = "h2", Watermark = 50, UpdatedAt = _t1, CreatedAt = _t1 };

        Assert.False((await _store.Save(relabeled, Ct)).IsError);
        var read = Assert.Single((await _store.All(Ct)).Value);

        Assert.Equal("Payment retries", read.Name);
        Assert.Equal("New summary.", read.Summary);
        Assert.Equal([new ConceptMember("rec-9", "1", "slack")], read.Members);
        Assert.Equal("h2", read.MembershipHash);
        Assert.Equal(50, read.Watermark);
        Assert.Equal(_t1, read.UpdatedAt);
        Assert.Equal(_t0, read.CreatedAt);
    }

    [Fact]
    public async Task Remove_deletes_only_that_key()
    {
        _ = await _store.Save(Concept("a"), Ct);
        _ = await _store.Save(Concept("b"), Ct);

        Assert.False((await _store.Remove("a", Ct)).IsError);
        var all = await _store.All(Ct);

        Assert.Equal(["b"], all.Value.Select(c => c.Key));
    }

    [Fact]
    public async Task Watermark_is_zero_until_saved_and_then_reads_back()
    {
        Assert.Equal(0, (await _store.Watermark(Ct)).Value);

        Assert.False((await _store.SaveWatermark(17, _t0, Ct)).IsError);
        Assert.False((await _store.SaveWatermark(23, _t1, Ct)).IsError);

        Assert.Equal(23, (await _store.Watermark(Ct)).Value);
    }
}
