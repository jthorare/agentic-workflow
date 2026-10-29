using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.CheckSandbox;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CheckSandbox;

/// <summary>Whoever runs the checks, the newest report is kept for the status read (sandbox spec §8): Check again and doctor go through the same port.</summary>
[Lane(TestLane.Unit)]
public sealed class RecordingSandboxChecksTests
{
    private static readonly DateTimeOffset _noon = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_run_keeps_its_report_with_the_time_it_finished()
    {
        var last = new LastSandboxCheck();
        var report = new SandboxCheckReport([SandboxBlocker.Of(SandboxRefusal.For(SindriErrors.LaunchSandboxUnavailable, SandboxReasons.NoBwrap))], []);
        var checks = new RecordingSandboxChecks(new Fixed(report), last, new FakeTimeProvider(_noon));

        var answered = await checks.Run(Token);

        Assert.Same(report, answered);
        Assert.Equal((_noon, report), (last.Latest!.At, last.Latest.Report));
    }

    [Fact]
    public async Task The_newest_run_replaces_the_one_before()
    {
        var last = new LastSandboxCheck();
        var clock = new FakeTimeProvider(_noon);
        var checks = new RecordingSandboxChecks(new Fixed(new SandboxCheckReport([], [])), last, clock);

        _ = await checks.Run(Token);
        clock.Advance(TimeSpan.FromMinutes(3));
        _ = await checks.Run(Token);

        Assert.Equal(_noon.AddMinutes(3), last.Latest!.At);
    }

    [Fact]
    public async Task The_check_again_service_says_when_the_checks_ran()
    {
        var last = new LastSandboxCheck();
        var checks = new RecordingSandboxChecks(new Fixed(new SandboxCheckReport([], [])), last, new FakeTimeProvider(_noon));

        var response = (await new CheckSandboxService(checks, last).Handle(new CheckSandboxRequest(), Token)).Value;

        Assert.Equal(_noon, response.CheckedAt);
    }

    [Fact]
    public async Task The_check_again_service_lists_the_steps_a_press_would_resume_with_their_item_and_title()
    {
        var instance = WorkflowInstanceId.Of(InstanceAddress.From("ITM-11111111111111111111111111111111/spec/1"));
        var parked = new ParkedOnSandbox(instance, _noon.AddHours(-2), "ITM-11111111111111111111111111111111", "Add the tax field");
        var report = new SandboxCheckReport([], [instance]) { Resume = [parked] };

        var response = (await new CheckSandboxService(new Fixed(report), new LastSandboxCheck()).Handle(new CheckSandboxRequest(), Token)).Value;

        var line = Assert.Single(response.Resume);
        Assert.Equal((instance, "ITM-11111111111111111111111111111111", "Add the tax field", _noon.AddHours(-2)), (line.Instance, line.Item, line.Title, line.Since));
        Assert.Null(response.CheckedAt);
    }

    private sealed class Fixed(SandboxCheckReport report) : ISandboxChecks
    {
        public ValueTask<SandboxCheckReport> Run(CancellationToken cancellationToken) => ValueTask.FromResult(report);
    }
}
