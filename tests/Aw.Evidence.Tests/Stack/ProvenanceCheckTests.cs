using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Stack;

public sealed class ProvenanceCheckTests
{
    private static async Task<ProvenanceFinding> Check(FakeProvenanceQuery query, SeedPolicy? policy = null) =>
        await ProvenanceCheck.RunAsync(query, policy ?? SeedPolicy.Default, database: null, CancellationToken.None);

    [Fact, Trait("Lane", "unit")]
    public async Task Only_seed_rows_on_seed_domains_are_seeded()
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
    public async Task One_real_looking_email_makes_the_data_unknown()
    {
        var finding = await Check(new FakeProvenanceQuery { Emails = { "seed-admin-1@example.com", "jane@hospital.org" } });

        Assert.Equal(DbProvenance.Unknown, finding.Provenance);
        Assert.Contains("jane@hospital.org", finding.Reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failing_query_is_unknown_with_its_reason() =>
        Assert.Equal(new ProvenanceFinding(DbProvenance.Unknown, "container not running"), await Check(new FakeProvenanceQuery { Failure = "container not running" }));

    [Fact, Trait("Lane", "unit")]
    public void Seed_settings_override_the_fail_closed_default_only_when_set()
    {
        Assert.Equal(SeedPolicy.Default.Domains, SeedPolicy.From(" ", emailPattern: null).Domains);
        Assert.Equal("seed-admin-%@example.com", SeedPolicy.From(domains: null, "").EmailPattern);
        var custom = SeedPolicy.From("seed.test, demo.test,", "admin-%@seed.test");
        Assert.Equal(["seed.test", "demo.test"], custom.Domains);
        Assert.Equal("admin-%@seed.test", custom.EmailPattern);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_without_an_email_makes_the_data_unknown()
    {
        var finding = await Check(new FakeProvenanceQuery { Emails = { "seed-admin-1@example.com", string.Empty } });

        Assert.Equal(DbProvenance.Unknown, finding.Provenance);
    }
}
