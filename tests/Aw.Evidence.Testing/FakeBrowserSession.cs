using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Design;
using Aw.Evidence.Core.Geometry;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Lint;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>An IBrowserSession over a <see cref="FakePage"/>; waits are immediate, every call is recorded, and a cancelled token throws before the call (as the adapter does).</summary>
/// <param name="page">The page state.</param>
/// <param name="options">The options it was opened with.</param>
public sealed class FakeBrowserSession(FakePage page, BrowserSessionOptions options) : IBrowserSession
{
    private readonly List<string> _calls = [];

    /// <summary>The page state.</summary>
    public FakePage Page { get; } = page;

    /// <summary>The options it was opened with.</summary>
    public BrowserSessionOptions Options { get; } = options;

    /// <summary>Every call, in order ('click testId:save', 'goto /x', …).</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Whether the session was disposed.</summary>
    public bool Disposed { get; private set; }

    /// <inheritdoc/>
    public string BrowserVersion => "140.0.0.0";

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> GotoAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"goto {path}");
        if (!StepGuard.IsSameOriginPath(path))
        {
            return Fail($"a goto target must be a path on the host: {path}");   // as the adapter refuses it
        }

        var parts = path.Split('?', 2);
        Page.Path = parts[0];
        Page.Query.Clear();
        foreach (var pair in parts.Length > 1 ? parts[1].Split('&', StringSplitOptions.RemoveEmptyEntries) : [])
        {
            var kv = pair.Split('=', 2);
            Page.Query[Uri.UnescapeDataString(kv[0])] = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
        }

        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ClickAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"click {FakePage.KeyOf(target)}");
        if (!Page.Has(target))
        {
            return Fail($"{FakePage.KeyOf(target)} not found");
        }

        if (Page.OnClick.TryGetValue(FakePage.KeyOf(target), out var effect))
        {
            effect(Page);
        }

        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> FillAsync(Locator target, string value, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"fill {FakePage.KeyOf(target)}");
        if (!Page.Has(target))
        {
            return Fail($"{FakePage.KeyOf(target)} not found");
        }

        Page.Inputs[InputKey(target)] = value;
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitVisibleAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Page.Has(target) ? Ok() : Fail($"{FakePage.KeyOf(target)} not visible");
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitHiddenAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Page.Has(target) ? Fail($"{FakePage.KeyOf(target)} still visible") : Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitForPathAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Page.Path == path ? Ok() : Fail($"path is {Page.Path}, not {path}");
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ScrollAsync(Locator target, string position, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"scroll {FakePage.KeyOf(target)} {position}");
        if (!Page.Has(target))
        {
            return Fail($"{FakePage.KeyOf(target)} not found");
        }

        if (Page.OnScroll.TryGetValue(FakePage.KeyOf(target), out var effect))
        {
            effect(Page);
        }

        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DragAsync(Locator source, Locator destination, DragGesture gesture, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"{FakePage.KeyOf(source)}>{FakePage.KeyOf(destination)}";
        _calls.Add($"drag {key}");
        if (!Page.Has(source) || !Page.Has(destination))
        {
            return Fail($"{key}: an end of the drag was not found");   // before the press: the mouse never goes down
        }

        if (Page.OnDrag.TryGetValue(key, out var effect))
        {
            effect(Page, gesture);
        }

        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> PressAsync(string key, Locator? target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(target is null ? $"press {key}" : $"press {key} on {FakePage.KeyOf(target)}");
        if (target is not null && !Page.Has(target))
        {
            return Fail($"{FakePage.KeyOf(target)} not found");   // no key is sent
        }

        Page.OnKey?.Invoke(Page, key);
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> WaitForUrlSearchAsync(string param, string value, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Page.Query.TryGetValue(param, out var actual) && actual == value ? Ok() : Fail($"url ?{param} is not {value}");
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ExpectAbsentNowAsync(Locator target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Page.Has(target) ? Fail($"{FakePage.KeyOf(target)} is visible") : Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> InputValueAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<string>>(Page.Has(target)
                ? (Page.Inputs.TryGetValue(InputKey(target), out var value) ? value : string.Empty)
                : EvidenceErrors.BrowserFailed.With($"{FakePage.KeyOf(target)} not found"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<Candidate>>> VisibleCandidatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Candidate> listed = [.. Page.Candidates];
        Page.OnEnumerate?.Invoke(Page);
        return ValueTask.FromResult(Results.Success(listed));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Box>> BoundingBoxAsync(Locator target, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<Box>>(Page.Boxes.TryGetValue(FakePage.KeyOf(target), out var box)
                ? box
                : EvidenceErrors.BrowserFailed.With($"{FakePage.KeyOf(target)} not found"));
    }

    /// <inheritdoc/>
    public ValueTask<Result<byte[]>> ScreenshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("screenshot");
        if (Page.ScreenshotThrows is { } thrown)
        {
            throw thrown;
        }

        return ValueTask.FromResult<Result<byte[]>>(Page.Screenshot.ToArray());
    }

    /// <inheritdoc/>
    public ValueTask<Result<PageSnapshot>> LintSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<Result<PageSnapshot>>(Page.Snapshot);
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SettleAsync(TimeSpan settle, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("settle");
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DropFocusAsync(Offset? neutralClick, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add(neutralClick is null ? "blur" : string.Create(CultureInfo.InvariantCulture, $"blur+click {neutralClick.X},{neutralClick.Y}"));
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> ResetScrollAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("reset-scroll");
        return Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> SignInAsync(LoginSpec login, Credentials credentials, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("sign-in");
        return Page.SignInFails ? Fail(credentials.Redact($"sign-in failed for {credentials.Email}")) : Ok();
    }

    /// <inheritdoc/>
    public ValueTask<Result<SessionRecording>> StopRecordingAsync(string tracePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"stop {tracePath}");
        var recording = Options.RecordDirectory is { } dir
            ? new SessionRecording(tracePath, [string.Create(CultureInfo.InvariantCulture, $"{dir.TrimEnd('/')}/video-{Options.Viewport.W}.webm")])
            : new SessionRecording(Trace: null, []);
        return ValueTask.FromResult<Result<SessionRecording>>(recording);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    private static string InputKey(Locator target) => target switch
    {
        TestIdLocator t => t.TestId.Value,
        CssLocator c => c.Css,
        _ => FakePage.KeyOf(target),
    };

    private static ValueTask<Result<Unit>> Ok() => ValueTask.FromResult(Results.Ok());

    private static ValueTask<Result<Unit>> Fail(string message) =>
        ValueTask.FromResult<Result<Unit>>(EvidenceErrors.BrowserFailed.With(message));
}
