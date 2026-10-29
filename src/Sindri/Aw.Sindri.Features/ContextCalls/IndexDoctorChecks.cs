using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Secrets;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Doctor's rows for the Sindri-owned record index (owned index spec, Part of #1394), so one 'sindri doctor' says what is missing before the index runs:
/// 'context:index-store' (the record store answers), 'context:index-github', 'context:index-linear' and 'context:index-slack' (the source's token pointer
/// resolves; never its value), 'context:index-ollama' (the records embedder's server answers with its model) and 'context:index-sync' (each source's last sync,
/// as 'sindri index status' shows it). With no approved profile, or a source the profile doesn't name, a row is 'ok ... not configured'. No call reaches GitHub, Linear or Slack.
/// </summary>
/// <param name="approvals">The approved profile.</param>
/// <param name="scopes">The engine's scope for the store and sync reads.</param>
/// <param name="engine">The context engine (settings check and feed progress).</param>
/// <param name="secrets">Resolves a token pointer, as the Host does for the engine.</param>
/// <param name="embedder">The records embedder's server.</param>
/// <param name="limit">How long the embedder look may take (3 s in the app).</param>
internal sealed class IndexDoctorChecks(IApprovedProfile approvals, ContextScopes scopes, IContextEngine engine, ISecretPointers secrets, IRecordEmbedderProbe embedder, TimeSpan limit) : IDoctorChecks
{
    internal const string StoreName = "context:index-store";
    internal const string GitHubName = "context:index-github";
    internal const string LinearName = "context:index-linear";
    internal const string SlackName = "context:index-slack";
    internal const string OllamaName = "context:index-ollama";
    internal const string SyncName = "context:index-sync";

    private const string NoProfile = "not configured (no profile is approved)";

    public async ValueTask<IReadOnlyList<DoctorCheck>> Run(CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } loaded)
        {
            return Unconfigured(NoProfile);
        }

        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        if (settings.IsError)
        {
            return [.. Unconfigured("not checked (the context settings are refused, see context:sources)")];
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var progress = await Progress(scope, cancellationToken).ConfigureAwait(false);
        var configured = settings.Value;
        return
        [
            Store(configured, progress),
            Token(GitHubName, "github", "context.sources.github.token", configured.GitHub?.Token, configured.GitHub is not null),
            Token(LinearName, "linear", "context.sources.linear.token", configured.Linear?.Token, configured.Linear is not null),
            Token(SlackName, "slack", "context.sources.slack.token", configured.Slack?.Token, configured.Slack is not null),
            await Ollama(configured, cancellationToken).ConfigureAwait(false),
            Sync(configured, progress),
        ];
    }

    private async ValueTask<Result<IReadOnlyList<SourceProgress>>> Progress(Result<ContextScope> scope, CancellationToken cancellationToken) =>
        scope.IsError ? scope.Error : await engine.Progress(scope.Value, cancellationToken).ConfigureAwait(false);

    private static bool AnySource(ContextSettings settings) => settings.GitHub is not null || settings.Linear is not null || settings.Slack is not null;

    private static DoctorCheck[] Unconfigured(string detail) =>
    [
        new(StoreName, CheckState.Ok, detail, Fix: null),
        new(GitHubName, CheckState.Ok, detail, Fix: null),
        new(LinearName, CheckState.Ok, detail, Fix: null),
        new(SlackName, CheckState.Ok, detail, Fix: null),
        new(OllamaName, CheckState.Ok, detail, Fix: null),
        new(SyncName, CheckState.Ok, detail, Fix: null),
    ];

    private static DoctorCheck Store(ContextSettings settings, Result<IReadOnlyList<SourceProgress>> progress)
    {
        if (!AnySource(settings))
        {
            return new DoctorCheck(StoreName, CheckState.Ok, "not configured (the approved profile names no github, linear or slack source)", Fix: null);
        }

        return progress.IsError
            ? new DoctorCheck(StoreName, CheckState.Fail, $"the record store doesn't answer: {progress.Error.Code} {progress.Error.Message}", "check $AW_SINDRI_HOME/context, then sindri index status")
            : new DoctorCheck(StoreName, CheckState.Ok, $"the record store answers ({progress.Value.Count} source feed(s))", Fix: null);
    }

    private DoctorCheck Token(string name, string source, string key, string? pointer, bool named)
    {
        if (!named)
        {
            return new DoctorCheck(name, CheckState.Ok, $"not configured (the approved profile names no context.sources.{source})", Fix: null);
        }

        if (SourceTokens.Look(pointer, secrets) is not { } resolved)
        {
            return new DoctorCheck(name, CheckState.Warn, $"{key} is not set, so {source} can't sync", $"set {key} to a secret pointer (sindri index connect {source})");
        }

        return resolved.IsError
            ? new DoctorCheck(name, CheckState.Fail, $"{resolved.Error.Code}: {resolved.Error.Message}", $"fix {key}'s target as the message says")
            : new DoctorCheck(name, CheckState.Ok, $"{pointer} resolves (not probed: the source is read by sindri index sync)", Fix: null);
    }

    private async ValueTask<DoctorCheck> Ollama(ContextSettings settings, CancellationToken cancellationToken)
    {
        if (!AnySource(settings))
        {
            return new DoctorCheck(OllamaName, CheckState.Ok, "not configured (no record source, so nothing to embed)", Fix: null);
        }

        const string Fix = "start Ollama and run: ollama pull nomic-embed-text:latest";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(limit);
        try
        {
            var probe = await embedder.Probe(timeout.Token).ConfigureAwait(false);
            return probe.IsError
                ? new DoctorCheck(OllamaName, CheckState.Warn, $"the records embedder isn't ready: {probe.Error.Message}", Fix)
                : new DoctorCheck(OllamaName, CheckState.Ok, $"Ollama answers at 127.0.0.1:11434 with {embedder.Model}", Fix: null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DoctorCheck(OllamaName, CheckState.Warn, $"Ollama did not answer within {limit.TotalSeconds.ToString("0", System.Globalization.CultureInfo.InvariantCulture)} s", Fix);
        }
    }

    private static DoctorCheck Sync(ContextSettings settings, Result<IReadOnlyList<SourceProgress>> progress)
    {
        if (!AnySource(settings))
        {
            return new DoctorCheck(SyncName, CheckState.Ok, "not configured (no record source, so nothing syncs)", Fix: null);
        }

        return progress.IsError ? new DoctorCheck(SyncName, CheckState.Ok, "no sync state to read yet (see context:index-store)", Fix: null) : IndexSyncRules.Judge(progress.Value);
    }
}
