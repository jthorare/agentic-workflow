using System.Text;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>The first lines of 'sindri open' and 'sindri takeover' for a sandboxed session (sandbox spec §7).</summary>
public sealed class SandboxFirstScreenTests
{
    private static ReadOnlyMemory<byte> Body(string json) => Encoding.UTF8.GetBytes(json);

    [Fact, Trait("Lane", "unit")]
    public void A_step_run_prints_the_takeover_banner() =>
        Assert.Equal(SandboxStatusLine.TakeoverBanner, SandboxFirstScreen.For(Body("""{"id":"l1","sandbox":{"mechanism":"bubblewrap"}}"""), "C-b"));

    [Fact, Trait("Lane", "unit")]
    public void An_attended_session_prints_the_attended_escape_with_its_own_prefix() =>
        Assert.Equal("Sandboxed (attended): push and secrets blocked. Outside it: Ctrl-a c", SandboxFirstScreen.For(Body("""{"id":"l1","sandbox":{"attended":true}}"""), "C-a"));

    [Theory, Trait("Lane", "unit")]
    [InlineData("""{"id":"l1"}""")]
    [InlineData("""{"id":"l1","sandbox":null}""")]
    [InlineData("not json")]
    public void An_unsandboxed_launch_prints_nothing(string body) => Assert.Null(SandboxFirstScreen.For(Body(body), "C-b"));

    [Fact, Trait("Lane", "unit")]
    public async Task An_attach_command_that_names_no_server_gives_the_usual_prefix() =>
        Assert.Equal("C-b", await SandboxFirstScreen.PrefixOf(attach: null, TestContext.Current.CancellationToken));
}
