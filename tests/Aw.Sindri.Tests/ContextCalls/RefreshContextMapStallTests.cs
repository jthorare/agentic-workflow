using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>#2106: a refresh job that writes no progress line for 30 min ends failed as timed out (SND-JOB-004) and is not retried; a job that keeps writing lines is never cut off, whatever its total time.</summary>
public sealed class RefreshContextMapStallTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly RefreshEngineProxy _engine = RefreshEngineProxy.Create();
    private readonly FakeJobContextAccessor _jobs = new();
    private readonly ScriptedOutlooks _outlooks = new();
    private readonly List<string> _progress = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Progress(List<string> lines) : IProgress<string>
    {
        public void Report(string value) => lines.Add(value);
    }

    private async Task<Kernel.Result<RefreshContextMapResult>> Run()
    {
        _h.Clock.SetUtcNow(_now);
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new RefreshContextMapService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), _engine.Proxy, _outlooks, new MapEventSource(_h.Held, _h.Store, _h.Events), _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("RefreshContextMap"), new Progress(_progress)));
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Handle(new RefreshContextMapRequest(), Ct);
    }

    // Moves the fake clock one minute at a time, up to the given number of minutes or until the run completes.
    private async Task Drive(Task run, int minutes)
    {
        for (var i = 0; i < minutes && !run.IsCompleted; i++)
        {
            _h.Clock.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(1, Ct);
        }
    }

    // A real-time bound so a run that never ends fails the assertion instead of hanging the test; a check that it is still running needs only a short look.
    private static async Task<bool> Completes(Task run, int milliseconds = 5000) => ReferenceEquals(run, await Task.WhenAny(run, Task.Delay(TimeSpan.FromMilliseconds(milliseconds), TimeProvider.System, Ct)));

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_writes_no_line_for_30_minutes_ends_the_job_timed_out()
    {
        _engine.BuildWith = async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return default!;
        };

        var run = Run();
        await Drive(run, 35);

        Assert.True(await Completes(run), "the job is still running 35 min after its last progress line");
        var result = await run;
        Assert.Equal("SND-JOB-004", result.Error.Code);
        Assert.Contains("30 min", result.Error.Message, StringComparison.Ordinal);
        Assert.Equal(1, _engine.Log.Count(static l => l == "BuildMap"));
        Assert.DoesNotContain(_progress, static l => l.StartsWith("retrying", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_keeps_writing_lines_is_not_cut_off()
    {
        _engine.BuildWith = async (progress, ct) =>
        {
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(TimeSpan.FromMinutes(20), _h.Clock, ct);
                progress?.Report("extracting");
            }

            return _engine.Built;
        };

        var run = Run();
        await Drive(run, 150);

        Assert.True(await Completes(run), "the job did not finish");
        var result = await run;
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_limit_counts_from_the_last_line_not_from_the_start()
    {
        _engine.BuildWith = async (progress, ct) =>
        {
            progress?.Report("extracting 0");
            await Task.Delay(TimeSpan.FromMinutes(25), _h.Clock, ct);
            progress?.Report("extracting 25");
            await Task.Delay(TimeSpan.FromMinutes(25), _h.Clock, ct);
            progress?.Report("extracting 50");
            await Task.Delay(Timeout.Infinite, ct);
            return default!;
        };

        var run = Run();
        await Drive(run, 79);

        Assert.False(await Completes(run, 300), "the job ended before 30 min passed since its last line (at minute 50)");
        await Drive(run, 5);
        Assert.True(await Completes(run), "the job is still running 34 min after its last progress line");
        Assert.Equal("SND-JOB-004", (await run).Error.Code);
    }
}
