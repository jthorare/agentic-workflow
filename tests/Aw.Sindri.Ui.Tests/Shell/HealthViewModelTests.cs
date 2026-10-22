using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

[Lane(TestLane.Unit)]
public sealed class HealthViewModelTests : IDisposable
{
    private readonly InMemoryEndpointRecords _records = new();
    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly HealthViewModel _vm;

    public HealthViewModelTests()
    {
        _vm = new HealthViewModel(_records, _server, _clock, new FakeClipboard());
    }

    private readonly InlineUiContext _ui = new();
    private Task _run = Task.CompletedTask;

    public void Dispose()
    {
        _stop.Cancel();
        Assert.True(_run.Wait(TimeSpan.FromSeconds(2), CancellationToken.None), "Run ended when cancelled");
        _stop.Dispose();
        _ui.Dispose();
    }

    private void Start() => _run = _vm.Run(_stop.Token);

    private static async Task Eventually(Func<bool> condition, string what)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), what);
    }

    [Fact]
    public async Task No_endpoint_record_reads_as_not_running_with_the_sindri_fix()
    {
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal("sindri", _vm.Fix);
        Assert.Equal(("memory:control.json", Labels.NoRecord), (_vm.ReasonDetail, _vm.Reason));
        Assert.False(_vm.IsRunning);
    }

    // F3/F4: the System page leads with one sentence, and the per-service detail waits behind Details.
    [Fact]
    public async Task Not_running_leads_with_one_plain_sentence()
    {
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal("Sindri isn't running.", _vm.Sentence);
    }

    [Fact]
    public async Task A_healthy_running_control_plane_says_everything_is_running()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Health = new HealthView("healthy", []) };
        Start();

        await Eventually(() => _vm.IsRunning, "running");
        Assert.Equal("Everything is running.", _vm.Sentence);
    }

    [Fact]
    public async Task Without_health_data_the_sentence_does_not_claim_all_is_well()
    {
        _records.Write(A.Record());
        Start();

        await Eventually(() => _vm.IsRunning, "running");
        Assert.Equal("Checking…", _vm.Sentence);
    }

    [Fact]
    public async Task A_pending_profile_fix_says_what_needs_you_even_when_health_is_healthy()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile()) with { Health = new HealthView("healthy", []) };
        Start();

        await Eventually(() => _vm.HasProfileFix, "profile fix");
        await Eventually(() => _vm.Sentence == "The profile needs your approval.", "the sentence");
    }

    [Fact]
    public async Task A_degraded_service_names_what_needs_a_look()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Health = new HealthView("degraded", [new HealthProblemView("SND-SCHED-001", "index failed")]) };
        Start();

        await Eventually(() => _vm.Sentence == "Something needs a look: index failed.", "the sentence");
    }

    [Fact]
    public async Task Degraded_without_problem_text_still_says_something_needs_a_look()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Health = new HealthView("degraded", []) };
        Start();

        await Eventually(() => _vm.Sentence == "Sindri is running, but something needs a look.", "the sentence");
    }

    [Fact]
    public void The_page_opens_on_overview() => Assert.True(_vm.ShowOverview);

    // API §6, §3.6 and the orchestrator's decision (PR #184): a call refused even after its one retry (SND-AUTH-001, a
    // missing or stale token) is the Unreadable banner with Sindri's code and detail as they are, never "nothing
    // answered"; the app keeps looking every 2 s, reading the token again, so it recovers with no restart.
    [Fact]
    public async Task A_refused_call_shows_sindris_detail_as_is_and_recovers_on_its_own()
    {
        _records.Write(A.Record());
        _server.Refusal = A.NoTokenRefusal;
        Start();

        await Eventually(() => _vm.State == ShellState.Unreadable, "the refusal");
        Assert.Equal($"{A.NoTokenRefusal.Code}: {A.NoTokenRefusal.Detail}", _vm.Reason);

        _server.Refusal = null;
        _clock.Advance(HealthViewModel.RetryEvery);
        await Eventually(() => _vm.IsRunning, "running again");
        Assert.Equal(ShellState.Normal, _vm.State);
    }

    // The merge of #184 with U1: any answer Sindri explains with its own code (not only a 403) is the Unreadable banner
    // with that code and detail; only SND-UI-002 (nothing answered) is Not running. The decision keys off the code.
    [Fact]
    public void A_refusal_that_isnt_a_403_is_unreadable_with_sindris_code_not_nothing_answered()
    {
        _ = _vm.Failed(new Error("SND-LEDGER-002", ErrorKind.Unavailable, "The ledger can't be read (fix: sindri doctor)"), new Uri("http://127.0.0.1:7410/"));

        Assert.Equal((ShellState.Unreadable, "SND-LEDGER-002: The ledger can't be read (fix: sindri doctor)"), (_vm.State, _vm.Reason));
    }

    // A token removed or replaced while the app follows: the next ping's refusal ends the follow (a definite answer, not
    // silence), so the window says why within one ping beat instead of only going stale.
    [Fact]
    public async Task A_ping_refused_while_following_shows_the_refusal()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.IsRunning, "running");

        _server.Refusal = A.NoTokenRefusal;
        _clock.Advance(HealthViewModel.PingEvery);

        await Eventually(() => _vm.State == ShellState.Unreadable, "the refusal");
        Assert.Equal($"{A.NoTokenRefusal.Code}: {A.NoTokenRefusal.Detail}", _vm.Reason);
    }

    // I7 (§4.1, §12.4): SND-UI-001's sentence leads; the record and the answer are one detail line under it.
    [Fact]
    public async Task A_record_for_another_process_reads_as_stale()
    {
        _records.Write(A.Record(pid: 1, epoch: 7));
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal((Labels.StaleRecord, "recorded pid 1 · epoch 7, answering pid 4242 · epoch 7"), (_vm.Reason, _vm.ReasonDetail));
    }

    // I7: the same process at another epoch (a restart that reused the pid) names the pid once, so it reads as no contradiction.
    [Fact]
    public async Task A_record_for_another_epoch_of_the_same_process_names_the_pid_once()
    {
        _records.Write(A.Record(pid: 69190, epoch: 4));
        _server.Pong = new(69190, 0);
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal((Labels.StaleRecord, "recorded pid 69190 · epoch 4, answering epoch 0"), (_vm.Reason, _vm.ReasonDetail));
    }

    // I7 (kill -9): SND-UI-002's sentence leads, the address is the detail line once, and no exception text shows.
    [Fact]
    public async Task A_record_nothing_answers_at_reads_as_not_running()
    {
        _records.Write(A.Record());
        _server.GoDown();
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal((Labels.NothingAnswered, "127.0.0.1:7410"), (_vm.Reason, _vm.ReasonDetail));
    }

    [Fact]
    public async Task Running_shows_the_lease_profile_and_services_as_the_cli_does()
    {
        _records.Write(A.Record());
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.True(_vm.IsRunning);
        Assert.Equal("pid 4242 · epoch 7", _vm.Lease);
        Assert.Equal("approved", _vm.ProfileState);
        Assert.Equal("hash ab12cd · /home/profile.yaml", _vm.ProfileDetail);
        Assert.Equal("", _vm.ProfileFix);
        Assert.Equal([new ServiceRow("heartbeat", "every 5s", "last run " + Local(A.Noon))], _vm.Services);
        Assert.Equal("127.0.0.1:7410", _vm.Api);
    }

    [Fact]
    public async Task The_health_line_words_the_status_in_4_8s_order()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile(), services: [A.Heartbeat(), A.Heartbeat()]) with { ApiVersion = 1 };
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal("Running · pid 4242 · epoch 7 · API v1 · 2 services · profile changed since approval", _vm.HealthLine);
    }

    [Fact]
    public async Task The_health_line_leaves_out_what_the_status_does_not_carry()
    {
        _records.Write(A.Record());
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal("Running · pid 4242 · epoch 7 · 1 service · profile approved", _vm.HealthLine);
    }

    [Fact]
    public async Task Not_running_is_the_whole_health_line()
    {
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Equal("Not running", _vm.HealthLine);
    }

    [Fact]
    public async Task A_profile_changed_since_approval_names_the_approve_fix()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile());
        Start();

        await Eventually(() => _vm.ProfileState == "changed since approval", "profile state");
        Assert.Equal("running with approved ab12cd; /home/profile.yaml is now ff00ee", _vm.ProfileDetail);
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
    }

    [Fact]
    public async Task An_unapproved_profile_names_the_approve_fix()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Unapproved());
        Start();

        await Eventually(() => _vm.ProfileState == "not approved", "profile state");
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
    }

    [Fact]
    public async Task The_approve_fix_follows_sindris_verdict_not_the_hashes()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Approved() with { NeedsApproval = true });
        Start();

        await Eventually(() => _vm.Headline == "Running", "running");
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
        Assert.True(_vm.HasProfileFix);
    }

    [Fact]
    public async Task An_event_refreshes_the_status_and_shows_as_the_last_event()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");

        _server.Current = A.Status(services: A.Heartbeat(A.Noon.AddSeconds(5)));
        _server.Push(A.Event(12, at: A.Noon.AddSeconds(5)));

        await Eventually(() => _vm.Services is [{ LastRun: var run }] && run == "last run " + Local(A.Noon.AddSeconds(5)), "heartbeat last run");
        Assert.Equal("service.heartbeat · " + Local(A.Noon.AddSeconds(5)) + " · #12", _vm.LastEvent);
    }

    [Fact]
    public async Task A_dropped_stream_goes_to_not_running_and_reconnects_from_its_cursor()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.Push(A.Event(30));
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _records.Remove();
        _server.GoDown();
        await Eventually(() => _vm.Headline == "Not running", "down");

        _server.ComeUp();
        _server.Pong = new(4242, 8);
        _server.Current = A.Status(epoch: 8);
        _records.Write(A.Record(epoch: 8));
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 4242 · epoch 8";
        }, "reconnected");
        Assert.Equal(30, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task A_restart_is_followed_while_the_old_stream_still_hangs_open()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");

        // The old control plane is stopping but holds its event stream open; a new one records another endpoint.
        _server.Pong = new(5151, 8);
        _server.Current = A.Status(pid: 5151, epoch: 8);
        _records.Write(A.Record(pid: 5151, epoch: 8, port: 7493));

        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 5151 · epoch 8";
        }, "followed the new endpoint");
        Assert.Equal("127.0.0.1:7493", _vm.Api);
    }

    [Fact]
    public async Task A_failed_refresh_ends_the_stream_instead_of_waiting_on_it()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        var streams = _server.StreamCursors.Count;

        _server.Current = A.Status(epoch: 7);
        _server.FailStatus = true;
        _server.Push(A.Event(40, "process.stopped"));
        await Eventually(() => _vm.Headline == "Not running", "status failed");
        _server.FailStatus = false;

        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _server.StreamCursors.Count > streams && _vm.Headline == "Running";
        }, "reopened the stream");
    }

    [Fact]
    public async Task An_unreadable_endpoint_record_reads_as_not_running_with_the_reason()
    {
        _records.WriteText("{not json");
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("can't be read", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_record_with_a_relative_url_reads_as_not_running()
    {
        _records.WriteText("""{"url":"sindri/","pid":4242,"epoch":7}""");
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("can't be read", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_status_without_the_lease_reads_as_not_running()
    {
        _records.Write(A.Record());
        _server.Current = A.Status() with { Running = false };
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("holds no lease", _vm.Reason, StringComparison.Ordinal);
    }

    // Review run 3 (security-sentinel-0): the card says why in the app's own words, never the platform's message, which
    // carries the record's full path; the path shows only shortened, on its own line (I5, I7).
    [Fact]
    public async Task A_record_the_platform_cant_read_reads_as_not_running_and_the_app_keeps_trying()
    {
        _records.Write(A.Record());
        _records.FailNextRead(EndpointRecordFailure.NoPermission);
        Start();

        await Eventually(() => _vm.Headline == "Not running", "not running");
        Assert.Equal(Labels.RecordUnreadableBecause(Labels.RecordNoPermission), _vm.Reason);
        Assert.Equal(StatusText.ShortPath(_records.Location), _vm.ReasonDetail);
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Headline == "Running";
        }, "recovered on the next try");
    }

    // Review run 4 (architecture-strategist-0): each reason the port names has its own words; a directory in the record's
    // place is "not a file", never "no permission" (which is what .NET's UnauthorizedAccessException alone would say).
    [Theory]
    [InlineData(EndpointRecordFailure.NotAFile, Labels.RecordNotAFile)]
    [InlineData(EndpointRecordFailure.Unreadable, Labels.RecordIoError)]
    internal async Task A_record_the_platform_fails_to_read_says_why_in_the_apps_words(EndpointRecordFailure why, string reason)
    {
        _records.Write(A.Record());
        _records.FailNextRead(why);
        Start();

        await Eventually(() => _vm.Headline == "Not running", "not running");
        Assert.Equal(Labels.RecordUnreadableBecause(reason), _vm.Reason);
        Assert.Equal(StatusText.ShortPath(_records.Location), _vm.ReasonDetail);
    }

    [Fact]
    public async Task A_new_ledger_after_a_reconnect_restarts_the_stream_from_the_start()
    {
        _records.Write(A.Record(epoch: 7));
        _server.Push(A.Event(30));
        Start();
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _records.Write(A.Record(epoch: 1));
        _server.Pong = new(4242, 1);
        _server.Current = A.Status(epoch: 1);
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 4242 · epoch 1";
        }, "reconnected to the new ledger");
        Assert.Equal(0, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task An_event_stream_that_ends_at_once_waits_before_reconnecting()
    {
        _records.Write(A.Record());
        _server.StreamsEndAtOnce = true;
        Start();
        await Eventually(() => _server.StreamCursors.Count == 1, "first stream opened");

        await Task.Delay(200, TestContext.Current.CancellationToken);
        _ = Assert.Single(_server.StreamCursors);

        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _server.StreamCursors.Count >= 2;
        }, "reconnected once the wait ended");
    }

    [Fact]
    public async Task A_record_with_a_non_http_url_reads_as_not_running()
    {
        _records.WriteText("""{"url":"file:///tmp/sindri/","pid":4242,"epoch":7}""");
        Start();

        await Eventually(() => _vm.Headline == "Not running", "headline");
        Assert.Contains("can't be read", _vm.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_ledger_at_the_same_epoch_restarts_the_stream_from_the_start()
    {
        _records.Write(A.Record(pid: 4242, epoch: 1));
        _server.Pong = new(4242, 1);
        _server.Current = A.Status(epoch: 1);
        _server.Push(A.Event(30));
        Start();
        await Eventually(() => _vm.LastEvent.EndsWith("#30", StringComparison.Ordinal), "event 30");

        _server.Pong = new(5151, 1);
        _server.Current = A.Status(pid: 5151, epoch: 1);
        _records.Write(A.Record(pid: 5151, epoch: 1));
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Lease == "pid 5151 · epoch 1";
        }, "followed the new process");
        Assert.Equal(0, _server.StreamCursors[^1]);
    }

    [Fact]
    public async Task A_profile_sindri_says_needs_approval_never_reads_as_approved()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.Approved() with { NeedsApproval = true });
        Start();

        await Eventually(() => _vm.Headline == "Running", "running");
        Assert.Equal("needs approval", _vm.ProfileState);
        Assert.Equal("sindri profile approve", _vm.ProfileFix);
    }

    [Fact]
    public async Task An_unreadable_live_profile_offers_no_approve_fix_because_approving_it_cant_work()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: new(Approved: null, Live: null, ChangedSinceApproval: false, Problem: "SND-PROFILE-002 bad key", Source: null, NeedsApproval: true));
        Start();

        await Eventually(() => _vm.ProfileState == "unreadable", "profile state");
        Assert.Equal("", _vm.ProfileFix);
        Assert.Equal("SND-PROFILE-002 bad key", _vm.ProfileDetail);
    }

    [Fact]
    public async Task A_record_that_turns_unreadable_during_a_follow_ends_the_follow()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");

        _records.FailNextRead(EndpointRecordFailure.NoPermission);
        await Eventually(() =>
        {
            _clock.Advance(HealthViewModel.RetryEvery);
            return _vm.Headline == "Running" && _server.StreamCursors.Count >= 2;
        }, "the watcher ended the follow and the app reconnected");
    }

    // §4.1: stale is "no event or ping for 30 s". A control plane with nothing scheduled (an unapproved profile) emits no
    // events, but it answers ping, so the views stay fresh however long it stays quiet.
    [Fact]
    public async Task A_quiet_control_plane_that_answers_ping_never_goes_stale()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        var pings = _server.PingCalls;

        for (var second = 1; second <= 90; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            var due = pings + (second / 10);
            await Eventually(() => _server.PingCalls >= due, "a ping every 10 s");
            Assert.DoesNotContain("stale", _vm.Pill.Text, StringComparison.Ordinal);
        }

        Assert.Equal(ShellState.Normal, _vm.State);
    }

    [Fact]
    public async Task Pings_that_fail_for_30_seconds_go_stale()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.FailPing = true;

        for (var second = 1; second < 30; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, TestContext.Current.CancellationToken);
            Assert.Equal(ShellState.Normal, _vm.State);
        }

        _clock.Advance(TimeSpan.FromSeconds(1));
        await Eventually(() => _vm.State == ShellState.Stale, "stale after 30 s of failed pings");
        await Eventually(() => _vm.Pill.Text == "stale · updated 30s ago", "the stale pill");
        Assert.Equal("stale · updated 30s ago", _vm.Pill.Text);
    }

    // A ping answered by another process or epoch isn't the control plane this follow connected to: it doesn't count.
    [Fact]
    public async Task A_ping_from_another_process_does_not_keep_the_views_fresh()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.Pong = new(5151, 9);

        for (var second = 1; second <= 30; second++)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }

        await Eventually(() => _vm.State == ShellState.Stale, "stale: the pings came from someone else");
    }

    // A good ping after a stale spell freshens the views again.
    [Fact]
    public async Task A_ping_that_answers_again_freshens_a_stale_view()
    {
        _records.Write(A.Record());
        Start();
        await Eventually(() => _vm.Headline == "Running", "running");
        _server.FailPing = true;
        _clock.Advance(HealthViewModel.StaleAfter);
        await Eventually(() => _vm.State == ShellState.Stale, "stale");

        _server.FailPing = false;
        await Eventually(() =>
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            return _vm.State == ShellState.Normal;
        }, "fresh again after a good ping");
    }

    private static string Local(DateTimeOffset t) => t.ToLocalTime().ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    // I4 (§4.2, §4.8): Home's strip is labelled cells, each value a word, in the health line's order.
    [Fact]
    public async Task The_health_strip_is_labelled_cells_in_the_health_lines_order()
    {
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile()) with { ApiVersion = 1 };
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal(
            [
                new HealthCell("Process", "Running · pid 4242"),
                new HealthCell("Epoch · lease", "7 · held"),
                new HealthCell("API", "v1"),
                new HealthCell("Services", "1 service"),
                new HealthCell("Profile", "changed since approval", StateGlyph.Profile),
            ],
            _vm.Detail.Strip);
    }

    [Fact]
    public async Task The_strip_leaves_out_the_API_cell_when_the_status_has_no_version()
    {
        _records.Write(A.Record());
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.DoesNotContain(_vm.Detail.Strip, c => c.Label == "API");
        Assert.Equal(new HealthCell("Profile", "approved", StateGlyph.Ok), _vm.Detail.Strip[^1]);
    }

    // I5 (§4.8): the Health page is label/value rows; times are local, and the home directory is ~.
    [Fact]
    public async Task The_Health_page_is_process_and_profile_rows_in_local_time()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var source = Path.Combine(home, ".sindri", "profile.yaml");
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile(source)) with { ApiVersion = 1 };
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal(
            [
                new HealthCell("pid · epoch", "4242 · 7"),
                new HealthCell("Host lease", "held · expires " + Local(A.Noon.AddSeconds(60))),
                new HealthCell("API", "127.0.0.1:7410 · v1"),
            ],
            _vm.Detail.Process);
        Assert.Equal(("changed since approval", StateGlyph.Profile), (_vm.Detail.ProfileHead, _vm.Detail.ProfileGlyph));
        Assert.Equal(
            [
                new HealthCell("approved", "ab12cd"),
                new HealthCell("live", "ff00ee"),
                new HealthCell("file", "~/.sindri/profile.yaml", IsPath: true),
            ],
            _vm.Detail.Profile);
        Assert.Equal([new ServiceRow("heartbeat", "every 5s", "last run " + Local(A.Noon))], _vm.Services);
    }

    // I5: a long profile path is shortened in the middle, keeping its file name (~ for home first).
    [Fact]
    public async Task A_long_profile_path_is_shortened_in_the_middle()
    {
        var source = "/srv/agentic/workspaces/team-alpha/projects/sindri-control-plane/profiles/default/profile.yaml";
        _records.Write(A.Record());
        _server.Current = A.Status(profile: A.ChangedProfile(source));
        Start();

        await Eventually(() => _vm.Headline == "Running", "headline");
        Assert.Equal(new HealthCell("file", "/srv/…/profiles/default/profile.yaml", IsPath: true), _vm.Detail.Profile[^1]);
    }
}
