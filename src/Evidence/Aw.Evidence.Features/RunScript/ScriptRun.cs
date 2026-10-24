using System.Globalization;
using Aw.Evidence.Core.Browser;
using Aw.Evidence.Core.Files;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Lint;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Scripts;
using Aw.Evidence.Core.Settings;
using Aw.Evidence.Core.Steps;
using Aw.Evidence.Core.Usage;
using Aw.Kernel;

namespace Aw.Evidence.Features.RunScript;

/// <summary>
/// Drives every step of a script for real, viewport by viewport, in a recorded session: a screenshot after each step,
/// a lint pass over each viewport's final page, then the trace and videos. A failed step never stops the run. A script's
/// own login signs in first, each viewport, with credentials read from the environment variables it names; its localStorage
/// entries are in place before the first page load.
/// </summary>
internal sealed class ScriptRun(IBrowser browser, StepRunner runner, IRunFiles files, IEvidenceSettings settings, ISecretsSource secrets)
{
    internal async ValueTask<Result<DrivenRun>> DriveAsync(UiScript script, RunTarget target, InvocationLog log, CancellationToken cancellationToken)
    {
        var (host, runDirectory) = (target.Host, target.RunDirectory);
        var run = new Collected();
        var credentials = ReadCredentials(script);
        if (credentials.IsError)
        {
            return credentials.Error;
        }

        var stepOptions = StepRunOptions.Run with { RefuseWrites = StepGuard.RunRefusesWrites(host, target.AllowWrites) };
        foreach (var viewport in script.Viewports)
        {
            var options = new BrowserSessionOptions(host, ScriptViewports.SizeOf(viewport), runDirectory, settings.UserAgent, script.LocalStorage);
            var opened = await browser.OpenAsync(options, cancellationToken).ConfigureAwait(false);
            if (opened.IsError)
            {
                return opened.Error;
            }

            await using (opened.Value.ConfigureAwait(false))
            {
                var driven = await ViewportAsync(opened.Value, new(script, viewport, runDirectory, log, stepOptions, credentials.Value), run, cancellationToken).ConfigureAwait(false);
                if (driven.IsError)
                {
                    return driven.Error;
                }
            }
        }

        return new DrivenRun(run.Steps, run.Lint, new RunEvidence(run.Traces, run.Videos), run.BrowserVersion ?? string.Empty);
    }

    private Result<Credentials?> ReadCredentials(UiScript script)
    {
        if (script.Login is not { } login)
        {
            return Results.Success<Credentials?>(value: null);
        }

        var read = Credentials.Read(secrets, login.EmailEnv, login.PasswordEnv);
        return read.IsError ? read.Error : Results.Success<Credentials?>(read.Value);
    }

    private static ValueTask<Result<Unit>> SignInAsync(IBrowserSession session, Pass pass, CancellationToken cancellationToken) =>
        pass.Script.Login is { } login && pass.Credentials is { } credentials
            ? session.SignInAsync(ScriptLoginSpec.For(login), credentials, cancellationToken)
            : ValueTask.FromResult(Results.Ok());

    private async ValueTask<Result<Unit>> ViewportAsync(IBrowserSession session, Pass pass, Collected run, CancellationToken cancellationToken)
    {
        run.BrowserVersion ??= session.BrowserVersion;
        var signedIn = await SignInAsync(session, pass, cancellationToken).ConfigureAwait(false);
        if (signedIn.IsError)
        {
            return signedIn.Error;
        }

        var name = ScriptViewports.NameOf(pass.Viewport);
        for (var i = 0; i < pass.Script.Steps.Count; i++)
        {
            var step = await StepAsync(session, pass, i + 1, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            run.Steps.Add(step.Value);
        }

        var snapshot = await session.LintSnapshotAsync(cancellationToken).ConfigureAwait(false);
        if (snapshot.IsError)
        {
            return snapshot.Error;
        }

        run.Lint.AddRange(PageLint.Check(snapshot.Value).Select(f => new RunLintFinding(pass.Viewport, f.Rule, f.Selector, f.Detail)));
        var recording = await session.StopRecordingAsync(RunPath.Combine(pass.RunDirectory, $"{name}-trace.zip"), cancellationToken).ConfigureAwait(false);
        if (recording.IsError)
        {
            return recording.Error;
        }

        if (recording.Value.Trace is { } trace)
        {
            run.Traces.Add(trace);
        }

        run.Videos.AddRange(recording.Value.Videos);
        return Unit.Value;
    }

    private async ValueTask<Result<RunStep>> StepAsync(IBrowserSession session, Pass pass, int index, CancellationToken cancellationToken)
    {
        var step = pass.Script.Steps[index - 1];
        var status = await runner.RunAsync(session, step, pass.Options, pass.Log, cancellationToken).ConfigureAwait(false);
        var shot = await session.ScreenshotAsync(cancellationToken).ConfigureAwait(false);
        if (shot.IsError)
        {
            return shot.Error;
        }

        var name = string.Create(CultureInfo.InvariantCulture, $"{ScriptViewports.NameOf(pass.Viewport)}-{index}-{step.ActionName}.png");
        var path = RunPath.Combine(pass.RunDirectory, name);
        var written = await files.WriteAsync(path, shot.Value, cancellationToken).ConfigureAwait(false);
        return written.Map(_ => new RunStep(pass.Viewport, index, step.Action, step.Subject, status, path));
    }

    private sealed record Pass(UiScript Script, ViewportKind Viewport, string RunDirectory, InvocationLog Log, StepRunOptions Options, Credentials? Credentials);

    private sealed class Collected
    {
        public List<RunStep> Steps { get; } = [];

        public List<RunLintFinding> Lint { get; } = [];

        public List<string> Traces { get; } = [];

        public List<string> Videos { get; } = [];

        public string? BrowserVersion { get; set; }
    }
}
