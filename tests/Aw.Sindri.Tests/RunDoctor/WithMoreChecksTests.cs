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

        public ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
        {
            Ran = true;
            return ValueTask.FromResult<IReadOnlyList<DoctorCheck>>([row]);
        }
    }
}
