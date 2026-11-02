using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>
/// The separate-login credential source (sandbox spec SB18, §3.4, SB-8): a target signs in once with its own account, and Steps use that agent home
/// ('$AW_SINDRI_HOME/agent-homes/&lt;target&gt;', made by 'sindri sandbox login') instead of a copy of the owner's. It is bound read-write at its own path, so a refresh
/// persists and every reader outside the sandbox finds the transcripts where the session wrote them. Nothing is copied, so there is nothing to release.
/// </summary>
internal sealed class SeparateLoginCredentialSource : IModelCredentialSource
{
    private const string ClaudeTarget = "claude-code";
    private const string CodexTarget = "codex";

    private readonly string _sindriHome;
    private readonly TimeProvider _clock;

    /// <summary>Creates the source.</summary>
    /// <param name="sindriHome">AW_SINDRI_HOME: the agent homes live under 'agent-homes/&lt;target&gt;'.</param>
    /// <param name="clock">The clock the expiry is read against.</param>
    internal SeparateLoginCredentialSource(string sindriHome, TimeProvider clock)
    {
        _sindriHome = sindriHome;
        _clock = clock;
    }

    /// <inheritdoc />
    public ModelLoginSource Id => ModelLoginSource.SeparateLogin;

    /// <inheritdoc />
    public bool Supports(AgentId target) => IsClaude(target) || IsCodex(target);

    /// <inheritdoc />
    public LoginStatus Probe(AgentId target)
    {
        if (IsClaude(target))
        {
            return ProbeFile(AgentHomes.For(_sindriHome, target) + "/.credentials.json", "Claude Code", LoginFiles.ClaudeExpiry, ClaudeRefreshToken, "open 'sindri sandbox login claude-code' to sign in again");
        }

        return IsCodex(target)
            ? ProbeFile(AgentHomes.For(_sindriHome, target) + "/auth.json", "Codex", LoginFiles.CodexExpiry, CodexRefreshToken, "open 'sindri sandbox login codex' to sign in again")
            : Status(LoginState.Missing);
    }

    /// <inheritdoc />
    public Task<Result<PreparedLogin>> Prepare(AgentId target, LaunchId launch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!Supports(target))
        {
            return Task.FromResult(Unavailable(SandboxReasons.SourceUnsupported, detail: null));
        }

        var status = Probe(target);
        if (status.State != LoginState.Ready)
        {
            return Task.FromResult(Unavailable(status.State == LoginState.Expired ? SandboxReasons.Expired : SandboxReasons.NoLogin, status.Detail));
        }

        var home = AgentHomes.For(_sindriHome, target);
        var variable = IsClaude(target) ? "CLAUDE_CONFIG_DIR" : "CODEX_HOME";
        Result<PreparedLogin> prepared = new PreparedLogin(
            ModelLoginSource.SeparateLogin,
            [new LoginMount(home, home, MountMode.ReadWrite)],
            new Dictionary<string, string>(StringComparer.Ordinal) { [variable] = home },
            [home],
            new LoginHandle("separate-login:" + launch.Value));   // why: only to tell two launches apart; there is no copy to clean
        return Task.FromResult(prepared);
    }

    /// <inheritdoc />
    public void Release(LoginHandle handle) => ArgumentNullException.ThrowIfNull(handle);   // why: the agent home is the target's own login and stays

    private static bool IsClaude(AgentId target) => string.Equals(target.Value, ClaudeTarget, StringComparison.Ordinal);

    private static bool IsCodex(AgentId target) => string.Equals(target.Value, CodexTarget, StringComparison.Ordinal);

    private static bool ClaudeRefreshToken(JsonElement root) =>
        root.TryGetProperty("claudeAiOauth", out var oauth) && HasString(oauth, "refreshToken");

    private static bool CodexRefreshToken(JsonElement root) =>
        root.TryGetProperty("tokens", out var tokens) && HasString(tokens, "refresh_token");

    private static bool HasString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 };

    private static LoginStatus Status(LoginState state, string? detail = null) => new(state, ModelLoginSource.SeparateLogin, detail);

    private static Result<PreparedLogin> Unavailable(string reason, string? detail) =>
        SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, reason, detail, ModelLoginSource.SeparateLogin);

    // Ready with its expiry when it is still valid, Expired when it has run out; a missing or unreadable file is no login.
    private LoginStatus ProbeFile(string file, string what, Func<JsonElement, DateTimeOffset?> expiry, Func<JsonElement, bool> hasRefreshToken, string expired)
    {
        if (!File.Exists(file))
        {
            return Status(LoginState.Missing, $"no {what} login in {Path.GetDirectoryName(file)}");
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var at = expiry(document.RootElement);

            // why: a Step uses this login in place and may refresh it, so only a login with no refresh token left has really run out (spec §3.4, §15)
            return at is { } when && when <= _clock.GetUtcNow() && !hasRefreshToken(document.RootElement)
                ? Status(LoginState.Expired, $"the {what} login has expired; {expired}")
                : new LoginStatus(LoginState.Ready, ModelLoginSource.SeparateLogin, ExpiresAt: at);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return Status(LoginState.Missing, $"the {what} login could not be read");
        }
    }
}
