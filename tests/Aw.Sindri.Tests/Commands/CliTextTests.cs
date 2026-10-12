using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

public sealed class CliTextTests
{
    private static readonly ProfileHash _hash = ProfileHash.From("3f1c2a" + new string('0', 58));
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7410"), 42, 3);

    [Fact, Trait("Lane", "unit")]
    public void The_summary_names_each_part_in_words()
    {
        var status = new GetStatusResponse(Running: true, 42, 3, KnownInstants.Epoch,
            new ProfileStatus(_hash, _hash, ChangedSinceApproval: false, Problem: null, "/home/profile.yaml", NeedsApproval: false),
            [new ServiceStatus(ServiceName.From("heartbeat"), "5s", KnownInstants.Epoch)]);

        var text = SummaryText.Render(status, _endpoint, showUi: true);

        Assert.Equal(string.Join('\n',
            "  profile   /home/profile.yaml (approved, hash 3f1c2a)",
            "  services  heartbeat every 5s (last 00:00:00Z)",
            "  api       127.0.0.1:7410",
            "  ui        not built yet (S1b); use sindri status",
            "  lease     held by pid 42, epoch 3",
            "  ready     running",
            string.Empty), text.ReplaceLineEndings("\n"));
        Assert.DoesNotContain('\u001b', text);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unapproved_profile_needs_attention_and_says_the_fix()
    {
        var status = new GetStatusResponse(Running: true, 42, 3, LeaseExpiresAt: null, new ProfileStatus(Approved: null, _hash, ChangedSinceApproval: false, Problem: null, "/p.yaml", NeedsApproval: true), []);

        var text = SummaryText.Render(status, _endpoint, showUi: false);

        Assert.Contains("running; the profile needs your approval", text, StringComparison.Ordinal);
        Assert.Contains("(not approved; fix: sindri profile approve)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("ui ", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Approving_without_a_terminal_is_refused_before_any_call()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var probe = new FakeControlPlaneProbe();
        using var client = new ControlPlaneClient(probe, SliceHarness.Json);
        var verbs = new Verbs(client, probe, new Output(stdout, stderr, SliceHarness.Json), new ServiceCollection().BuildServiceProvider());

        var code = await verbs.Approve(_hash.Value, interactive: false, new StringReader(_hash.Abbreviated + "\n"), TestContext.Current.CancellationToken);

        Assert.Equal(2, code);
        Assert.StartsWith("SND-PROFILE-004 approving needs an interactive terminal", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Status_when_nothing_runs_is_attention_with_the_fix()
    {
        var stderr = new StringWriter();
        var probe = new FakeControlPlaneProbe();
        using var client = new ControlPlaneClient(probe, SliceHarness.Json);
        var verbs = new Verbs(client, probe, new Output(new StringWriter(), stderr, SliceHarness.Json), new ServiceCollection().BuildServiceProvider());

        var code = await verbs.Status(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Equal("SND-API-001 not running: no control plane is recorded (fix: sindri)\n", stderr.ToString().ReplaceLineEndings("\n"));
    }
}
