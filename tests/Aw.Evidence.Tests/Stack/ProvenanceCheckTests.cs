using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class ProvenanceCheckTests
{
    private static async Task<ProvenanceFinding> Check(FakeProvenanceQuery query, SeedPolicy? policy = null) =>
        await ProvenanceCheck.RunAsync(query, policy ?? SeedPolicy.Default, database: null, CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task A_seed_row_makes_the_data_seeded()
    {
        var query = new FakeProvenanceQuery { Emails = { "seed-admin-1@example.com", "nurse@example.com" } };

        var finding = await Check(query);

        Assert.Equal(DbProvenance.Seeded, finding.Provenance);
        Assert.Equal(["seed-admin-%@example.com"], query.Patterns);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_seed_row_is_unknown()
    {
        var finding = await Check(new FakeProvenanceQuery { Emails = { "nurse@example.com" } });

        Assert.Equal(new ProvenanceFinding(DbProvenance.Unknown, "no seed row found in the seed table"), finding);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Faker_emails_on_real_looking_domains_beside_a_seed_row_stay_seeded()
    {
        var finding = await Check(new FakeProvenanceQuery { Emails = { "seed-admin-1@example.com", "Lavada.Kuhn@yahoo.com", string.Empty } });

        Assert.Equal(DbProvenance.Seeded, finding.Provenance);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_query_is_unknown_with_its_reason() =>
        Assert.Equal(new ProvenanceFinding(DbProvenance.Unknown, "container not running"), await Check(new FakeProvenanceQuery { Failure = "container not running" }));

    [Fact, Trait("Lane", "unit")]
    public void Seed_settings_override_the_fail_closed_default_only_when_set()
    {
        Assert.Equal(SeedPolicy.Default, SeedPolicy.From(" "));
        Assert.Equal(SeedPolicy.Default, SeedPolicy.From(emailPattern: null));
        Assert.Equal("admin-%@seed.test", SeedPolicy.From("admin-%@seed.test").EmailPattern);
    }
}
