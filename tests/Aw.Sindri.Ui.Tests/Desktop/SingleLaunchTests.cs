using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class SingleLaunchTests : IDisposable
{
    private readonly FakeInstanceChannel _channel = new();
    private readonly StringWriter _errors = new();
    private readonly RecordingLogger _log = new();

    public void Dispose() => _errors.Dispose();

    [Fact]
    public void The_first_launch_runs_the_app()
    {
        var first = new FakeSingleInstance(_channel);

        Assert.Equal(7, SingleLaunch.Run(first, [], () => 7, _errors, _log));
    }

    [Fact]
    public async Task A_second_launch_sends_focus_and_exits_0_without_running()
    {
        var first = new FakeSingleInstance(_channel);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        await using var reader = new InstanceReader(first, TestContext.Current.CancellationToken);
        var ran = false;

        var exit = SingleLaunch.Run(new FakeSingleInstance(_channel), [], () =>
        {
            ran = true;
            return 0;
        }, _errors, _log);

        Assert.Equal(0, exit);
        Assert.False(ran);
        Assert.Equal([InstanceMessages.Focus], await reader.Take(1));
    }

    // The running instance holds the claim but doesn't handle the message (a failed start-up, a wedged app): the later
    // launch doesn't report success, and names the process to quit.
    [Fact]
    public async Task A_second_launch_to_an_instance_that_does_not_answer_exits_1_naming_its_process()
    {
        var first = new FakeSingleInstance(_channel, processId: 4242);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        var ran = false;

        var exit = new SingleLaunch(TimeSpan.FromMilliseconds(100)).Execute(new FakeSingleInstance(_channel), [], () =>
        {
            ran = true;
            return 0;
        }, _errors, _log);

        Assert.Equal(1, exit);
        Assert.False(ran);
        Assert.Equal("sindri-ui: Sindri app is running but not answering (pid 4242): quit it or kill it" + Environment.NewLine, _errors.ToString());
    }

    // The holder ended between this launch's claim and its message: this launch claims after all and runs the app.
    [Fact]
    public async Task A_second_launch_takes_over_when_the_holder_ends_before_it_answers()
    {
        var first = new FakeSingleInstance(_channel);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        var second = new EndsHolderOnSend(new FakeSingleInstance(_channel), first);

        Assert.Equal(7, SingleLaunch.Run(second, [], () => 7, _errors, _log));
        Assert.Equal(string.Empty, _errors.ToString());
    }

    // Review run 5 (julik-frontend-races-reviewer-0): a relaunch while the app is quitting is not answered "handled"
    // (nothing would run) and not told the holder "isn't answering": the holder retires, the message is refused, and the
    // launch waits for the holder to go, then claims and runs the app.
    [Fact]
    public async Task A_second_launch_while_the_holder_quits_waits_for_it_to_go_and_runs_the_app()
    {
        var first = new FakeSingleInstance(_channel);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        await using var reader = new InstanceReader(first, TestContext.Current.CancellationToken);
        first.Retire();
        // The holder goes only after this launch's claim failed once inside the wait (its first claim and the refused message come before).
        var second = new EndsHolderOnClaim(new FakeSingleInstance(_channel), first, claimsBefore: 2);
        var ran = false;

        var exit = new SingleLaunch(TimeSpan.FromSeconds(5)).Execute(second, [], () =>
        {
            ran = true;
            return 7;
        }, _errors, _log);

        Assert.Equal(7, exit);
        Assert.True(ran);
        Assert.Equal(string.Empty, _errors.ToString());
        Assert.Equal(0, _channel.Taken);
        Assert.Equal(3, second.Claims);
    }

    // A holder that retires but never goes (wedged while ending) is still named after the bounded wait.
    [Fact]
    public async Task A_second_launch_to_a_holder_that_quits_but_never_goes_exits_1_after_the_wait()
    {
        var first = new FakeSingleInstance(_channel, processId: 4242);
        Assert.True(await first.TryClaim(TestContext.Current.CancellationToken));
        first.Retire();

        var exit = new SingleLaunch(TimeSpan.FromMilliseconds(100)).Execute(new FakeSingleInstance(_channel), [], () => 0, _errors, _log);

        Assert.Equal(1, exit);
        Assert.Contains("pid 4242", _errors.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new string[0], InstanceMessages.Focus)]
    [InlineData(new[] { "item/ITM-01JC8" }, "item/ITM-01JC8")]
    [InlineData(new[] { "sindri://item/ITM-01JC8" }, "item/ITM-01JC8")]
    [InlineData(new[] { "sindri://projects" }, "projects")]
    [InlineData(new[] { "sindri://setup" }, "setup")]
    [InlineData(new[] { "--verbose" }, InstanceMessages.Focus)]
    [InlineData(new[] { "item/a", "item/b" }, InstanceMessages.Focus)]
    public void A_later_launch_sends_its_deep_link_or_focus(string[] args, string message) =>
        Assert.Equal(message, SingleLaunch.MessageFor(args));

    [Theory]
    [InlineData("projects", true)]
    [InlineData("item/x", true)]
    [InlineData("nonsense", false)]
    public void Only_a_link_is_forwarded(string text, bool link) => Assert.Equal(link, InstanceMessages.IsLink(text));

    // The app ends with 2 when it failed (SindriApp logs the cause); the launch says so on standard error. Review run 4
    // (code-reviewer-1): the words are neutral ("failed"), since the app may have run normally before it failed.
    [Fact]
    public void A_failed_app_exits_2_with_a_line_on_stderr()
    {
        Assert.Equal(2, SingleLaunch.Run(new FakeSingleInstance(_channel), [], () => 2, _errors, _log));

        Assert.Equal("sindri-ui: failed (SND-UI-004); the log names the cause unless AW_LOG_LEVEL is none" + Environment.NewLine, _errors.ToString());
    }

    // A failure before the UI starts (the 'ui' directory can't be made: a file sits there) is the documented SND-UI-004:
    // the code and the exception's type in the log, one line on standard error, exit 2. Never the path, which names the
    // user's home, and never a stack trace.
    [Fact]
    public void A_failure_before_the_UI_starts_exits_2_with_SND_UI_004_and_no_path()
    {
        var ran = false;

        var exit = SingleLaunch.Run(new FailsToClaim(new IOException("The file '/Users/someone/.agentic-workflow/sindri-app/ui' already exists.")), [], () =>
        {
            ran = true;
            return 0;
        }, _errors, _log);

        Assert.Equal(2, exit);
        Assert.False(ran);
        // Lead (1): true whether or not a log line printed (AW_LOG_LEVEL=none): the cause is named on the line itself.
        Assert.Equal("sindri-ui: failed (SND-UI-004: IOException)" + Environment.NewLine, _errors.ToString());
        var entry = Assert.Single(_log.Entries);
        Assert.Equal("SND-UI-004", entry.EventName);
        Assert.Equal("SND-UI-004 the Sindri app failed (IOException) and exits with code 2", entry.Message);
        Assert.DoesNotContain("/Users/someone", entry.Message, StringComparison.Ordinal);
        Assert.Null(entry.Exception);
    }

    // The same for a failure in the hand-over to the running instance (a socket it can't reach for a reason other than "nothing listens").
    [Fact]
    public void A_failure_handing_over_exits_2_with_SND_UI_004()
    {
        var exit = SingleLaunch.Run(new FailsToSend(new UnauthorizedAccessException("Access to the path '/Users/someone/ui/instance.sock' is denied.")), [], () => 0, _errors, _log);

        Assert.Equal(2, exit);
        Assert.DoesNotContain("/Users/someone", _errors.ToString(), StringComparison.Ordinal);
        Assert.Equal("SND-UI-004", Assert.Single(_log.Entries).EventName);
    }

    // Review run 3 (code-reviewer-2): ending the instance after the app quit is inside the boundary too. A failure there
    // (ui/ removed while the app ran) is SND-UI-004 with the type only and exit 2, never an unhandled exception whose
    // message and stack name the home.
    [Fact]
    public void A_failure_ending_the_instance_exits_2_with_SND_UI_004_and_no_path()
    {
        var instance = new FailsToEnd(new FakeSingleInstance(_channel), new DirectoryNotFoundException("Could not find a part of the path '/Users/someone/.agentic-workflow/sindri-app/ui/instance.sock'."));

        var exit = SingleLaunch.Run(instance, [], () => 0, _errors, _log);

        Assert.Equal(2, exit);
        Assert.True(instance.Ended);
        Assert.Equal("sindri-ui: failed (SND-UI-004: DirectoryNotFoundException)" + Environment.NewLine, _errors.ToString());
        Assert.Equal("SND-UI-004", Assert.Single(_log.Entries).EventName);
        Assert.DoesNotContain("/Users/someone", Assert.Single(_log.Entries).Message, StringComparison.Ordinal);
    }

    // Composing the platform (the first OS motion read, the adapters) is inside the boundary as well, and the launch
    // ends the instance and the motion poll it composed.
    [Fact]
    public void A_failure_composing_the_platform_exits_2_with_SND_UI_004()
    {
        var exit = SingleLaunch.Run(() => throw new UnauthorizedAccessException("Access to the path '/Users/someone/ui' is denied."), [], _ => 0, _errors, _log);

        Assert.Equal(2, exit);
        Assert.Equal("sindri-ui: failed (SND-UI-004: UnauthorizedAccessException)" + Environment.NewLine, _errors.ToString());
        Assert.Equal("SND-UI-004", Assert.Single(_log.Entries).EventName);
    }

    [Fact]
    public void The_launch_runs_the_app_over_the_platform_it_composed_and_ends_its_instance_and_motion_poll()
    {
        var instance = new FailsToEnd(new FakeSingleInstance(_channel), failure: null);
        var motion = new EndsMotion(failure: null);
        var platform = new SindriPlatform(new InMemoryEndpointRecords(), new InMemoryHumanToken(), new InMemoryPreferencesStore(), motion, instance, new FakeTray());
        SindriPlatform? ran = null;

        var exit = SingleLaunch.Run(() => platform, [], p =>
        {
            ran = p;
            return 7;
        }, _errors, _log);

        Assert.Equal(7, exit);
        Assert.Same(platform, ran);
        Assert.True(instance.Ended);
        Assert.True(motion.Ended);
    }

    // Review run 4 (pattern-recognition-specialist-4): ending the motion poll is inside the SND-UI-004 boundary too, so a
    // failure there is the documented line and exit 2, never an unhandled exception out of Main.
    [Fact]
    public void A_failure_ending_the_motion_poll_exits_2_with_SND_UI_004()
    {
        var motion = new EndsMotion(new InvalidOperationException("the poll's timer is gone"));
        var platform = new SindriPlatform(new InMemoryEndpointRecords(), new InMemoryHumanToken(), new InMemoryPreferencesStore(), motion, new FakeSingleInstance(_channel), new FakeTray());

        var exit = SingleLaunch.Run(() => platform, [], _ => 0, _errors, _log);

        Assert.Equal(2, exit);
        Assert.True(motion.Ended);
        Assert.Equal("sindri-ui: failed (SND-UI-004: InvalidOperationException)" + Environment.NewLine, _errors.ToString());
        Assert.Equal("SND-UI-004", Assert.Single(_log.Entries).EventName);
    }

    // A motion poll the launch ends (the desktop's polls the OS on a timer), whose end fails or not, and says whether it
    // was ended.
    private sealed class EndsMotion(Exception? failure) : IMotionPreference, IDisposable
    {
        private readonly FakeMotionPreference _inner = new();

        public event EventHandler? Changed
        {
            add => _inner.Changed += value;
            remove => _inner.Changed -= value;
        }

        public bool Ended { get; private set; }

        public bool ReduceMotion => _inner.ReduceMotion;

        public void Dispose()
        {
            Ended = true;
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    // An instance whose end fails (or not, with no failure), and says whether it was ended.
    private sealed class FailsToEnd(ISingleInstance inner, Exception? failure) : ISingleInstance
    {
        public bool Ended { get; private set; }

        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => inner.TryClaim(cancellationToken);

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => inner.Send(message, cancellationToken);

        public IAsyncEnumerable<InstanceMessage> Messages(CancellationToken cancellationToken) => inner.Messages(cancellationToken);

        public void Retire() => inner.Retire();

        public int? HolderProcessId() => inner.HolderProcessId();

        public async ValueTask DisposeAsync()
        {
            Ended = true;
            await inner.DisposeAsync();
            if (failure is not null)
            {
                throw failure;
            }
        }
    }

    private sealed class FailsToClaim(Exception failure) : ISingleInstance
    {
        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromException<bool>(failure);

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public IAsyncEnumerable<InstanceMessage> Messages(CancellationToken cancellationToken) => AsyncEnumerable.Empty<InstanceMessage>();

        public void Retire()
        {
        }

        public int? HolderProcessId() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailsToSend(Exception failure) : ISingleInstance
    {
        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => ValueTask.FromResult(false);

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => ValueTask.FromException<bool>(failure);

        public IAsyncEnumerable<InstanceMessage> Messages(CancellationToken cancellationToken) => AsyncEnumerable.Empty<InstanceMessage>();

        public void Retire()
        {
        }

        public int? HolderProcessId() => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    // A later launch whose holder ends just before its claim number claimsBefore + 1, and that counts its claims.
    private sealed class EndsHolderOnClaim(ISingleInstance inner, ISingleInstance holder, int claimsBefore) : ISingleInstance
    {
        public int Claims { get; private set; }

        public async ValueTask<bool> TryClaim(CancellationToken cancellationToken)
        {
            if (++Claims == claimsBefore + 1)
            {
                await holder.DisposeAsync();
            }

            return await inner.TryClaim(cancellationToken);
        }

        public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => inner.Send(message, cancellationToken);

        public IAsyncEnumerable<InstanceMessage> Messages(CancellationToken cancellationToken) => inner.Messages(cancellationToken);

        public void Retire() => inner.Retire();

        public int? HolderProcessId() => inner.HolderProcessId();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    // A later launch whose message arrives just as the holder ends.
    private sealed class EndsHolderOnSend(ISingleInstance inner, ISingleInstance holder) : ISingleInstance
    {
        public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => inner.TryClaim(cancellationToken);

        public async ValueTask<bool> Send(string message, CancellationToken cancellationToken)
        {
            await holder.DisposeAsync();
            return await inner.Send(message, cancellationToken);
        }

        public IAsyncEnumerable<InstanceMessage> Messages(CancellationToken cancellationToken) => inner.Messages(cancellationToken);

        public void Retire() => inner.Retire();

        public int? HolderProcessId() => inner.HolderProcessId();

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
