using Aw.Kernel;
using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The contract (<see cref="SindriApiContract"/>, continued): the sandbox's reads and its Allow write, as the spec-shaped goldens serve them (wire contract items 3, 4, 8) until the server's are recorded.</summary>
public abstract partial class SindriApiContract
{
    [Fact]
    public async Task SandboxStatus_never_checked_has_no_time_and_no_verdict()
    {
        var server = new FakeSindriApi { SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "never-checked") };

        var status = (await Reach(server).SandboxStatus(_api, Token)).Value;

        Expect.Equal((null, null, 0), (status.CheckedAt, status.Ready, status.Blockers.Count));
        Expect.Equal(1, server.SandboxStatusCalls);
    }

    [Fact]
    public async Task SandboxStatus_blocked_carries_the_blocker_in_the_owners_words()
    {
        var server = new FakeSindriApi { SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "blocked") };

        var status = (await Reach(server).SandboxStatus(_api, Token)).Value;

        Expect.Equal(1, status.Blockers.Count);
        var blocker = status.Blockers[0];
        Expect.Equal((false, "bubblewrap", "SND-LAUNCH-014", "no-bwrap"), (status.Ready, status.Mechanism, blocker.Code, blocker.Reason));
        Expect.Equal("sudo apt install bubblewrap", blocker.Command);
    }

    [Fact]
    public async Task SandboxStatus_learning_lists_the_open_window_and_the_limits()
    {
        var server = new FakeSindriApi { SandboxStatusAnswer = RouteAnswer.SpecShaped("sandbox-status", "learning") };

        var status = (await Reach(server).SandboxStatus(_api, Token)).Value;

        Expect.Equal(1, status.Learn.Count);
        var learn = status.Learn[0];
        Expect.Equal(("api", DateTimeOffset.Parse("2026-10-31T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture)), (learn.Repo, learn.EndsAt));
        Expect.Equal(("12GiB", "40m", "owner-copy"), (status.Limits.MemoryMax, status.Limits.Wall, status.Login));
    }

    [Fact]
    public async Task SandboxCheck_still_failing_has_the_blocker_and_nothing_to_resume()
    {
        var server = new FakeSindriApi();

        var check = (await Reach(server).SandboxCheck(_api, Token)).Value;

        Expect.Equal((false, 1, 0), (check.Ready, check.Blockers.Count, check.Resume?.Count));
        Expect.Equal(1, server.SandboxCheckCalls);
    }

    [Fact]
    public async Task SandboxCheck_all_clear_lists_the_parked_steps_a_resume_would_start()
    {
        var server = new FakeSindriApi { SandboxCheckAnswer = RouteAnswer.SpecShaped("sandbox-check", "all-clear-with-resume") };

        var check = (await Reach(server).SandboxCheck(_api, Token)).Value;

        Expect.Equal((true, 0), (check.Ready, check.Blockers.Count));
        Expect.Sequence(["ITM-0000000000000000000000000000000f/step/1", "ITM-00000000000000000000000000000010/step/1"], check.Resume!.Select(r => r.Instance));
        Expect.Equal("Try the engine", check.Resume![0].Title);
        Expect.True(check.CheckedAt is not null);
    }

    [Fact]
    public async Task SandboxDenials_asks_for_the_item_and_reads_each_fix_kind()
    {
        var server = new FakeSindriApi();

        var denials = (await Reach(server).SandboxDenials(_api, "ITM-0000000000000000000000000000000f", Token)).Value;

        Expect.Sequence(["ITM-0000000000000000000000000000000f"], server.SandboxDenialItems);
        Expect.Equal(1, denials.Launches.Count);
        var launch = denials.Launches[0];
        Expect.Equal("api", launch.Repo);
        Expect.Sequence(["allow", "allow", "never", "own-terminal"], launch.Entries.Select(e => e.Fix));
        Expect.Equal("Keys and tokens are never shared with a session.", launch.Entries[2].Reason);
    }

    [Fact]
    public async Task SandboxDenials_many_keeps_every_entry_and_the_overflow_count()
    {
        var server = new FakeSindriApi { SandboxDenialsAnswer = RouteAnswer.SpecShaped("sandbox-denials", "many") };

        var launch = (await Reach(server).SandboxDenials(_api, "ITM-0000000000000000000000000000000f", Token)).Value.Launches[0];

        Expect.Equal((8, 42), (launch.Entries.Count, launch.Overflow.Single().Count));
    }

    [Fact]
    public async Task SandboxAllow_sends_the_request_and_answers_needs_approval()
    {
        var server = new FakeSindriApi();
        var request = new AllowRequest("LCH-00000000000000000000000000000001", "network", "registry.example.org");

        var allowed = (await Reach(server).SandboxAllow(_api, request, Token)).Value;

        Expect.Equal(("needs-approval", "api", "registry.example.org"), (allowed.State, allowed.Repo, allowed.Entry));
        Expect.Sequence([request], server.Allowed);
    }

    [Fact]
    public async Task SandboxAllow_of_a_permanent_path_is_refused_with_the_sandbox_code()
    {
        var server = new FakeSindriApi { SandboxAllowAnswer = RouteAnswer.Problem("sandbox-allow", "refused") };

        var request = new AllowRequest("LCH-00000000000000000000000000000001", "path", "/home/thor/.ssh");
        var result = await Reach(server).SandboxAllow(_api, request, Token);

        Expect.Equal(("SND-SANDBOX-001", ErrorKind.Conflict), (result.Error.Code, result.Error.Kind));
        Expect.Sequence([request], server.Allowed);   // asked once, and the refusal is the answer
    }

    [Fact]
    public async Task SandboxAllow_by_a_session_is_refused()
    {
        var server = new FakeSindriApi { SandboxAllowAnswer = RouteAnswer.Problem("sandbox-allow", "refused-session") };

        var result = await Reach(server).SandboxAllow(_api, new AllowRequest("LCH-00000000000000000000000000000001", "network", "registry.example.org"), Token);

        Expect.Equal(("SND-AUTH-002", ErrorKind.Forbidden), (result.Error.Code, result.Error.Kind));
    }
}
