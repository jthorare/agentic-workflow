using Aw.Architecture.Tests.Rules;
using Aw.Architecture.Tests.WriteKeyFixtures;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-LINEAR-001 (Linear hub spec 2026-11-04 D3): only the Linear write adapter names the write key type, so no read path can reach the secret.</summary>
[Trait("Lane", "integration")]
public sealed class WriteKeyRulesTests
{
    private const string Key = "Aw.Context.Infrastructure.Linear.LinearWriteKey";
    private const string Holder = "Aw.Context.Infrastructure.Linear.LinearWriteClient";

    private static IEnumerable<Violation> Check(IEnumerable<Type> types) => WriteKeyRules.Check(types, typeof(KeyFixture).FullName ?? string.Empty, typeof(HolderFixture).FullName ?? string.Empty);

    [Fact]
    public void In_the_context_infrastructure_only_the_write_adapter_names_the_write_key()
    {
        var infrastructure = Production.Assemblies.Single(static a => string.Equals(a.GetName().Name, "Aw.Context.Infrastructure", StringComparison.Ordinal));
        Assert.NotNull(infrastructure.GetType(Key));
        Assert.NotNull(infrastructure.GetType(Holder));

        Verify.None(WriteKeyRules.Check(infrastructure.GetTypes(), Key, Holder));
    }

    [Fact]
    public void The_holder_and_the_key_itself_and_unrelated_types_are_not_reported() =>
        Verify.None(Check([typeof(KeyFixture), typeof(HolderFixture), typeof(UnrelatedFixture)]));

    [Fact]
    public void A_type_with_the_key_in_a_member_is_reported() =>
        Verify.Exactly(Check([typeof(KeyFixture), typeof(HolderFixture), typeof(FieldReaderFixture)]), WriteKeyRules.Id, nameof(FieldReaderFixture));

    [Fact]
    public void A_type_that_only_builds_the_key_in_a_closure_is_reported() =>
        Verify.Exactly(Check([typeof(KeyFixture), typeof(HolderFixture), typeof(CallerFixture), .. typeof(CallerFixture).GetNestedTypes(System.Reflection.BindingFlags.NonPublic)]), WriteKeyRules.Id, nameof(CallerFixture));
}
