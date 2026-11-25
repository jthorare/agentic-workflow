using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Features.RunDoctor;

namespace Aw.Sindri.Tests.RunDoctor;

[Lane(TestLane.Unit)]
public sealed class WithMoreChecksTests
{
    private static readonly DoctorCheck _ok = new("lease", CheckState.Ok, "free", Fix: null);

    private static readonly TimeSpan _cap = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task More_rows_follow_the_core_rows_in_registration_order_and_the_exit_rule_covers_them_all()
    {
        var doctor = new WithMoreChecks(new Core([_ok], exitCode: 0),
            [new More(new DoctorCheck("context:map", CheckState.Ok, "ok", Fix: null)), new More(new DoctorCheck("context:mcp-scope", CheckState.Warn, "026", "remove it"))]);

        var report = (await doctor.Handle(new RunDoctorRequest(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["lease", "context:map", "context:mcp-scope"], report.Checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal(1, report.ExitCode);   // why: a warn from the appended rows, though the core alone exited 0
    }

    [Fact]
    public async Task A_fail_in_the_appended_rows_exits_2()
    {
        var doctor = new WithMoreChecks(new Core([_ok], exitCode: 0), [new More(new DoctorCheck("context:sources", CheckState.Fail, "011", "status"))]);

        Assert.Equal(2, (await doctor.Handle(new RunDoctorRequest(), TestContext.Current.CancellationToken)).Value.ExitCode);
    }

    [Fact]
    public async Task A_core_error_passes_through_and_no_more_checks_run()
    {
        var more = new More(_ok);
        var doctor = new WithMoreChecks(new Core(SindriErrors.ProfileMissing.With("x")), [more]);

        var report = await doctor.Handle(new RunDoctorRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(SindriErrors.ProfileMissing.Code, report.Error.Code);
        Assert.False(more.Ran);
    }

    [Fact]
    public async Task The_check_classes_run_at_the_same_time()
    {
        var first = new Waits("first");
        var second = new Waits("second");
        first.Other = second.Started;
        second.Other = first.Started;   // why: each waits for the other to start, so a pass that ran them one after another would never end
        var doctor = new WithMoreChecks(new Core([_ok], exitCode: 0), [first, second]);

        var report = (await doctor.Handle(new RunDoctorRequest(), TestContext.Current.CancellationToken).AsTask().WaitAsync(_cap, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["lease", "first", "second"], report.Checks.Select(c => c.Name), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_row_waits_for_the_class_before_it_even_when_its_own_class_finished_first()
    {
        var seen = new List<string>();
        var held = new Held();
        var later = new More(new DoctorCheck("later", CheckState.Ok, "ok", Fix: null));
        var doctor = new WithMoreChecks(new Core([_ok], exitCode: 0), [held, later]);

        var handled = doctor.Handle(new RunDoctorRequest { OnRow = row => { lock (seen) { seen.Add(row.Name); } } }, TestContext.Current.CancellationToken).AsTask();
        await later.Done.Task.WaitAsync(_cap, TestContext.Current.CancellationToken);
        lock (seen)
        {
            Assert.Equal(["lease"], seen, StringComparer.Ordinal);   // why: the core row streamed at once; "later" is done but must not print before "held"
        }

        held.Release.SetResult();
        _ = await handled.WaitAsync(_cap, TestContext.Current.CancellationToken);

        Assert.Equal(["lease", "held", "later"], seen, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_harness_probe_runs_in_the_same_pass_and_its_rows_come_last()
    {
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var check = new Waits("context:map") { Other = probeStarted.Task };   // why: the class finishes only once the probe started, so the probe can't run after the pass
        var doctor = new WithMoreChecks(new Core([_ok], exitCode: 0), [check]);
        var request = new RunDoctorRequest
        {
            Harnesses = _ =>
            {
                probeStarted.SetResult();
                return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([new DoctorCheck("harness:claude", CheckState.Warn, "re-probed", Fix: null)]);
            },
        };

        var report = (await doctor.Handle(request, TestContext.Current.CancellationToken).AsTask().WaitAsync(_cap, TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["lease", "context:map", "harness:claude"], report.Checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public async Task A_class_that_throws_becomes_one_fail_row_and_the_others_still_report()
    {
        var doctor = new WithMoreChecks(new Core([_ok], exitCode: 0), [new Throws(), new More(new DoctorCheck("fast", CheckState.Ok, "ok", Fix: null))]);

        var report = (await doctor.Handle(new RunDoctorRequest(), TestContext.Current.CancellationToken)).Value;

        Assert.Equal(["lease", "Throws", "fast"], report.Checks.Select(c => c.Name), StringComparer.Ordinal);
        Assert.Equal(CheckState.Fail, report.Checks[1].State);
        Assert.Contains("InvalidOperationException", report.Checks[1].Detail, StringComparison.Ordinal);
        Assert.Equal(2, report.ExitCode);
    }

    private sealed class Waits(string name) : IDoctorChecks
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public Task Other { get; set; } = Task.CompletedTask;

        public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
        {
            _started.SetResult();
            await Other.WaitAsync(cancellationToken).ConfigureAwait(false);
            return [new DoctorCheck(name, CheckState.Ok, "ok", Fix: null)];
        }
    }

    private sealed class Held : IDoctorChecks
    {
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
        {
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return [new DoctorCheck("held", CheckState.Ok, "ok", Fix: null)];
        }
    }

    private sealed class Throws : IDoctorChecks
    {
        public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken) => throw new InvalidOperationException("broken");
    }

    private sealed class Core : IService<RunDoctorRequest, RunDoctorResponse>
    {
        private readonly Result<RunDoctorResponse> _answer;

        public Core(IReadOnlyList<DoctorCheck> checks, int exitCode)
        {
            _answer = new RunDoctorResponse(checks, exitCode);
        }

        public Core(Error error)
        {
            _answer = error;
        }

        public ValueTask<Result<RunDoctorResponse>> Handle(RunDoctorRequest request, CancellationToken cancellationToken) => ValueTask.FromResult(_answer);
    }

    private sealed class More(DoctorCheck row) : IDoctorChecks
    {
        public bool Ran { get; private set; }

        public TaskCompletionSource Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
        {
            Ran = true;
            _ = Done.TrySetResult();
            return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([row]);
        }
    }
}
