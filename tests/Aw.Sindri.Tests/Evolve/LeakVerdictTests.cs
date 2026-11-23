using System.Reflection;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Evolve;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Evolve;

/// <summary>A hold names its layer only: the verdict type has no place for matched text, and its reason never holds any.</summary>
[Lane(TestLane.Unit)]
public sealed class LeakVerdictTests
{
    [Fact]
    public void The_verdict_type_carries_no_text_but_its_fixed_reason()
    {
        var properties = typeof(LeakVerdict).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var fields = typeof(LeakVerdict).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.All(fields, static f => Assert.True(f.FieldType == typeof(LeakLayer?), $"{f.Name} is {f.FieldType}"));
        Assert.Equal("Reason", Assert.Single(properties, static p => p.PropertyType == typeof(string)).Name);
        Assert.False(typeof(LeakVerdict).GetProperty("Reason")?.CanWrite);
    }

    [Fact]
    public void Each_layer_reason_is_fixed_words_that_name_the_layer()
    {
        foreach (var layer in Enum.GetValues<LeakLayer>())
        {
            var reason = LeakVerdict.HeldBy(layer).Reason;
            Assert.Contains(layer.ToString().ToLowerInvariant(), reason, StringComparison.Ordinal);
            Assert.Equal(reason, LeakVerdict.HeldBy(layer).Reason);
        }
    }

    [Fact]
    public void A_clean_verdict_has_an_empty_reason()
    {
        Assert.False(LeakVerdict.Clean.Held);
        Assert.Equal(string.Empty, LeakVerdict.Clean.Reason);
    }

    [Fact]
    public void A_term_hold_reason_does_not_contain_the_term()
    {
        var rules = new PrivacyRules(["Zorblax"], [], [], new HashSet<string>(StringComparer.Ordinal));
        var rewrite = Proposals.Saying("Ask before touching Zorblax billing");
        var verdict = new LeakGate(new PassThroughScrubber()).Check(new GateInput(rewrite, rewrite, JudgeFoundLeak: false, []), rules);

        Assert.Equal(LeakLayer.Terms, verdict.Layer);
        Assert.DoesNotContain("zorblax", verdict.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("billing", verdict.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
