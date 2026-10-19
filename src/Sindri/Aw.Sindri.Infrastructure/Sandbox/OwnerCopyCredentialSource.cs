using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The owner-copy credential source (sandbox spec SB18, §3.1, §3.4; spike item 2): the session gets the owner's own login as a per-launch 0600 copy in a directory
/// bound at '~/.claude-run' or '~/.codex-run', and the CLI is pointed at it. The copy is never synced back, and the original stays read-only with its login file masked.
/// A refresh may rotate the token and kill the owner's login, so a session never refreshes: a login that expires within the run's planned time is -017 'expired'.
/// </summary>
internal sealed class OwnerCopyCredentialSource : IModelCredentialSource
{
    private const string ClaudeTarget = "claude-code";
    private const string CodexTarget = "codex";
    private const string StaleMark = ".prepared";

    // What a session writes in its own config directory instead of the owner's: everything else in the agent home is bound read-only into it.
    // The Claude Code list is the write set of spike item 2, minus the two login files (rename-written, so they live in the copy directory); a test compares it with the spike.
    private static readonly string[] _claudeScratch = ["projects", "sessions", "session-env", "shell-snapshots", "file-history", "todos", "history.jsonl", "backups", "cache", "state", "mcp-needs-auth-cache.json", ".last-update-result.json", ".last-cleanup"];
    private static readonly string[] _codexScratch = ["sessions", "log", "memories", "shell_snapshots", "cache", "tmp"];

    private readonly OwnerCopyOptions _options;
    private readonly TimeProvider _clock;
    private readonly string _claudeHome;
    private readonly string _codexHome;
    private readonly string _launches;

    /// <summary>Creates the source.</summary>
    /// <param name="options">Where the logins are and where the copies go.</param>
    /// <param name="clock">The clock the expiry is read against.</param>
    internal OwnerCopyCredentialSource(OwnerCopyOptions options, TimeProvider clock)
    {
        _options = options;
        _clock = clock;
        _claudeHome = options.Home + "/.claude";
        _codexHome = options.Home + "/.codex";
        _launches = options.SindriHome + "/launches";
    }

    private enum AgentKind
    {
        None,
        Claude,
        Codex,
    }

    /// <summary>Gets the names the Claude Code session writes in its own config directory (the owner's copies are not bound).</summary>
    internal static IReadOnlyList<string> ClaudeScratch => _claudeScratch;

    /// <inheritdoc />
    public ModelLoginSource Id => ModelLoginSource.OwnerCopy;

    /// <inheritdoc />
    public bool Supports(AgentId target) => Kind(target) != AgentKind.None;

    /// <inheritdoc />
    public LoginStatus Probe(AgentId target) => Kind(target) switch
    {
        AgentKind.Claude => ProbeClaude(),
        AgentKind.Codex => ProbeCodex(),
        AgentKind.None => Status(LoginState.Missing),
        _ => Status(LoginState.Missing),
    };

    /// <inheritdoc />
    public Task<Result<PreparedLogin>> Prepare(AgentId target, LaunchId launch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var kind = Kind(target);
        if (kind == AgentKind.None)
        {
            return Task.FromResult(Unavailable(SandboxReasons.SourceUnsupported, detail: null));
        }

        var status = Probe(target);
        if (status.State != LoginState.Ready)
        {
            return Task.FromResult(Unavailable(status.State == LoginState.Expired ? SandboxReasons.Expired : SandboxReasons.NoLogin, status.Detail));
        }

        var directory = $"{_launches}/{launch.Value}/login";
        try
        {
            return Task.FromResult(kind == AgentKind.Claude ? CopyClaude(directory) : CopyCodex(directory));
        }
        catch (IOException)
        {
            Release(new LoginHandle(directory));
            return Task.FromResult(Unavailable(SandboxReasons.NoLogin, "the login could not be copied"));
        }
        catch (UnauthorizedAccessException)
        {
            Release(new LoginHandle(directory));
            return Task.FromResult(Unavailable(SandboxReasons.NoLogin, "the login could not be read"));
        }
    }

    /// <inheritdoc />
    public void Release(LoginHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!handle.Value.StartsWith(_launches + "/", StringComparison.Ordinal) || !handle.Value.EndsWith("/login", StringComparison.Ordinal) || handle.Value.Contains("..", StringComparison.Ordinal))
        {
            return;   // not a copy this source made
        }

        TryDelete(handle.Value);
        var parent = Path.GetDirectoryName(handle.Value);
        if (parent is not null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            TryDelete(parent);
        }
    }

    /// <summary>
    /// Removes the copies of launches that no longer have a live scope: the crash sweep at serve start and on Check again. A launch is live from <see cref="Prepare"/> until
    /// <see cref="Release"/>, so a copy younger than <see cref="OwnerCopyOptions.SweepGrace"/> is left alone even when no scope exists yet.
    /// </summary>
    /// <param name="isLive">Whether the launch with this id (the directory name) still has a live scope.</param>
    /// <returns>How many copies were removed.</returns>
    internal int SweepStale(Func<string, bool> isLive)
    {
        ArgumentNullException.ThrowIfNull(isLive);
        if (!Directory.Exists(_launches))
        {
            return 0;
        }

        var removed = 0;
        foreach (var launch in Directory.EnumerateDirectories(_launches))
        {
            var login = launch + "/login";
            if (Directory.Exists(login) && !isLive(Path.GetFileName(launch)) && _clock.GetUtcNow() - PreparedAt(login) >= OwnerCopyOptions.SweepGrace)
            {
                Release(new LoginHandle(login));
                removed++;
            }
        }

        return removed;
    }

    private static AgentKind Kind(AgentId target) =>
        string.Equals(target.Value, ClaudeTarget, StringComparison.Ordinal) ? AgentKind.Claude : string.Equals(target.Value, CodexTarget, StringComparison.Ordinal) ? AgentKind.Codex : AgentKind.None;

    private static LoginStatus Status(LoginState state, string? detail = null) => new(state, ModelLoginSource.OwnerCopy, detail);

    // The copy directory's last write is stamped when Prepare finishes, with the source's own clock.
    private static DateTimeOffset PreparedAt(string login) => new(File.GetLastWriteTimeUtc(login + "/" + StaleMark), TimeSpan.Zero);

    private static LoginStatus Corrupt(string what) => Status(LoginState.Missing, what + " could not be read");

    private static IEnumerable<LoginMount> ReadOnlyEntries(string home, string config, string[] scratch) =>
        Directory.EnumerateFileSystemEntries(home)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => !scratch.Contains(name, StringComparer.Ordinal) && !IsLoginFile(name))
            .Order(StringComparer.Ordinal)
            .Select(name => new LoginMount($"{home}/{name}", $"{config}/{name}", MountMode.ReadOnly));

    // The login files are rename-written inside their directory and never bound as files; the backups of ~/.claude.json carry the same content.
    private static bool IsLoginFile(string name) =>
        name.StartsWith(".credentials.json", StringComparison.Ordinal) || name.StartsWith(".claude.json", StringComparison.Ordinal) || string.Equals(name, "auth.json", StringComparison.Ordinal);

    private static Result<PreparedLogin> Prepared(List<LoginMount> mounts, string variable, string config, string directory) =>
        new PreparedLogin(ModelLoginSource.OwnerCopy, mounts, new Dictionary<string, string>(StringComparer.Ordinal) { [variable] = config }, [config], new LoginHandle(directory));

    // Written like the CLI writes it, a 0600 temp file in the directory and a rename.
    private static void CopyFile(string from, string to) => OwnerOnly.WriteAtomically(to, File.ReadAllBytes(from));

    // Marks when the copy was made, on this source's clock, for the crash sweep's grace period.
    private void Stamp(string directory)
    {
        var mark = directory + "/" + StaleMark;
        File.WriteAllText(mark, string.Empty);
        File.SetLastWriteTimeUtc(mark, _clock.GetUtcNow().UtcDateTime);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // already gone, or in use: the crash sweep tries again
        }
        catch (UnauthorizedAccessException)
        {
            // the sweep tries again
        }
    }

    private static Result<PreparedLogin> Unavailable(string reason, string? detail) =>
        SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, reason, detail, ModelLoginSource.OwnerCopy);

    private LoginStatus ProbeClaude()
    {
        var file = _claudeHome + "/.credentials.json";
        if (!File.Exists(file))
        {
            return Status(LoginState.Missing, "no Claude Code login in " + _claudeHome);
        }

        return Parsed(file, "the Claude Code login", LoginFiles.ClaudeExpiry, "the Claude Code login has expired; open Claude Code once to refresh it");
    }

    private LoginStatus ProbeCodex()
    {
        var file = _codexHome + "/auth.json";
        if (!File.Exists(file))
        {
            return Status(LoginState.Missing, "no Codex login in " + _codexHome);
        }

        return Parsed(file, "the Codex login", LoginFiles.CodexExpiry, "the Codex login is older than Codex's refresh window; sign in to Codex again");
    }

    // Ready with its expiry when it is still valid, Expired when it has run out; a file that cannot be read is no login.
    private LoginStatus Parsed(string file, string what, Func<JsonElement, DateTimeOffset?> expiry, string expired)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var at = expiry(document.RootElement);
            return at is { } when && when <= _clock.GetUtcNow() ? Status(LoginState.Expired, expired) : new LoginStatus(LoginState.Ready, ModelLoginSource.OwnerCopy, ExpiresAt: at);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return Corrupt(what);
        }
    }

    private Result<PreparedLogin> CopyClaude(string directory)
    {
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(directory)!);   // why: the copy directory always has a parent
        OwnerOnly.EnsureDirectory(directory);
        Stamp(directory);   // first: a sweep that meets the copy mid-Prepare sees it as new
        CopyFile(_claudeHome + "/.credentials.json", directory + "/.credentials.json");
        if (File.Exists(_options.Home + "/.claude.json") && LoginFiles.WithoutMcpSecrets(File.ReadAllBytes(_options.Home + "/.claude.json")) is { } settings)
        {
            OwnerOnly.WriteAtomically(directory + "/.claude.json", settings);   // the owner's MCP servers keep their names, not the tokens they were given
        }

        var config = _options.Home + "/.claude-run";
        List<LoginMount> mounts = [new(_claudeHome, _claudeHome, MountMode.ReadOnly), new(directory, config, MountMode.ReadWrite)];
        mounts.AddRange(ReadOnlyEntries(_claudeHome, config, _claudeScratch));
        return Prepared(mounts, "CLAUDE_CONFIG_DIR", config, directory);
    }

    private Result<PreparedLogin> CopyCodex(string directory)
    {
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(directory)!);   // why: the copy directory always has a parent
        OwnerOnly.EnsureDirectory(directory);
        Stamp(directory);   // first: a sweep that meets the copy mid-Prepare sees it as new
        CopyFile(_codexHome + "/auth.json", directory + "/auth.json");
        var config = _options.Home + "/.codex-run";
        List<LoginMount> mounts = [new(_codexHome, _codexHome, MountMode.ReadOnly), new(directory, config, MountMode.ReadWrite)];
        mounts.AddRange(ReadOnlyEntries(_codexHome, config, _codexScratch));
        if (Directory.Exists(_codexHome + "/sessions"))
        {
            mounts.Add(new LoginMount(_codexHome + "/sessions", config + "/sessions", MountMode.ReadWrite));   // Sindri's readers find Codex transcripts in the owner's sessions
        }

        return Prepared(mounts, "CODEX_HOME", config, directory);
    }
}
