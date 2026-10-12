using Aw.Judge.Core.Providers;
using Aw.Judge.Infrastructure.Providers;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Providers;

/// <summary>The boundary backstop the catalog puts around each adapter: an adapter's bug becomes a recorded failure.</summary>
[Lane(TestLane.Unit)]
public sealed class FailOpenModelProviderTests
{
    [Fact]
    public async Task A_provider_that_throws_is_errored_provider_threw()
    {
        var provider = new FailOpenModelProvider(new ScriptedProvider(ProviderName.Jev).Throws());

        var result = await provider.Decide(Questions.Text, Questions.Input, Questions.Budget, TestContext.Current.CancellationToken);

        Assert.Equal(new ProviderErrored("provider-threw"), result);
    }

    [Fact]
    public async Task The_callers_cancellation_still_throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var provider = new FailOpenModelProvider(new ScriptedProvider(ProviderName.Jev).Decides("send", 1));

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await provider.Decide(Questions.Text, Questions.Input, Questions.Budget, cancelled.Token));
    }

    [Fact]
    public void It_is_the_provider_it_wraps()
    {
        var inner = new ScriptedProvider(ProviderName.CodexCli, ContentClass.Image);
        var provider = new FailOpenModelProvider(inner);

        Assert.Equal((ProviderName.CodexCli, inner.Classes), (provider.Name, provider.Classes));
    }
}
