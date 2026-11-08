using Aw.Kernel.Testing;
using Aw.Redaction;
using Aw.Sindri.Infrastructure.Text;

namespace Aw.Sindri.Tests.Memory;

[Lane(TestLane.Unit)]
public sealed class MemoryRedactionTests
{
    [Fact]
    public async Task A_secret_in_fact_text_is_scrubbed_before_it_is_stored()
    {
        var h = new MemoryHarness { Scrubber = new RedactingScrubber(Redactor.Default) };
        await h.Memory();
        var secret = "gh" + "p_" + new string('a', 36);   // a GitHub token shape the redactor knows, built so no literal sits in the source

        var written = await h.Write().Handle(MemoryHarness.Fact($"The deploy token is {secret}"), TestContext.Current.CancellationToken);

        Assert.True(written.IsSuccess);
        var stored = Assert.Single(h.Store.AllFacts);
        Assert.DoesNotContain(secret, stored.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, string.Concat(h.Embedder.Seen), StringComparison.Ordinal);   // the model never sees it either
    }
}
