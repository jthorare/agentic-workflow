using Aw.Context.Core.ConceptScan;
using Xunit;

namespace Aw.Context.Testing.Map;

/// <summary>The concept hint store's contract: the fake and the SQLite store pass the same tests.</summary>
public abstract class ConceptHintStoreContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset _at = DateTimeOffset.Parse("2026-10-25T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A version nobody scanned reads as no hints.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_version_nobody_scanned_has_no_hints()
    {
        const string test = nameof(A_version_nobody_scanned_has_no_hints);
        var store = CreateStore();

        Expect.That(Expect.Ok(await store.Read(7, None), test).Count == 0, test, "an unscanned version read as having hints");
    }

    /// <summary>Hints read back whole and in order, and a second save of the version replaces the first.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Saved_hints_read_back_whole_and_a_second_save_replaces_them()
    {
        const string test = nameof(Saved_hints_read_back_whole_and_a_second_save_replaces_them);
        var store = CreateStore();
        var first = new ConceptHint("Self scheduling", ["feature:shifts.swap", "feature:shifts.open"], ["src/shifts/swap.ts"], 0.8);
        var second = new ConceptHint("Payroll", ["feature:pay.run"], [], 0.5);

        _ = Expect.Ok(await store.Save(7, [first, second], _at, None), test);
        var read = Expect.Ok(await store.Read(7, None), test);
        _ = Expect.Ok(await store.Save(7, [second], _at, None), test);
        var replaced = Expect.Ok(await store.Read(7, None), test);

        Expect.That(read.Count == 2 && read[0].Name == "Self scheduling" && read[0].Members.SequenceEqual(first.Members, StringComparer.Ordinal)
            && read[0].Evidence.SequenceEqual(first.Evidence, StringComparer.Ordinal) && read[0].Confidence == 0.8 && read[1].Name == "Payroll", test, "the hints changed on the way back");
        Expect.That(replaced.Count == 1 && replaced[0].Name == "Payroll", test, "the second save didn't replace the first");
    }

    /// <summary>One version's hints are not another's.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Hints_belong_to_their_version()
    {
        const string test = nameof(Hints_belong_to_their_version);
        var store = CreateStore();

        _ = Expect.Ok(await store.Save(7, [new ConceptHint("Payroll", ["feature:pay.run"], [], 0.5)], _at, None), test);

        Expect.That(Expect.Ok(await store.Read(8, None), test).Count == 0, test, "another version read the hints");
    }

    /// <summary>Creates a fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract IConceptHintStore CreateStore();
}
