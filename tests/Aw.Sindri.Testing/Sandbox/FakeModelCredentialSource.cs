using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sandbox;
using Aw.Sindri.Core.Sandbox.Credentials;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Testing.Sandbox;

/// <summary>An <see cref="IModelCredentialSource"/> whose logins the test declares; it records what was prepared and released.</summary>
/// <remarks>Creates a source with <paramref name="id"/>.</remarks>
/// <param name="id">The source id.</param>
public sealed class FakeModelCredentialSource(ModelLoginSource id) : IModelCredentialSource
{
    private readonly Dictionary<AgentId, LoginState> _logins = [];
    private readonly List<LoginHandle> _prepared = [];
    private readonly List<LoginHandle> _released = [];

    /// <inheritdoc />
    public ModelLoginSource Id { get; } = id;

    /// <summary>The handles <see cref="Prepare"/> made.</summary>
    public IReadOnlyList<LoginHandle> Prepared => _prepared;

    /// <summary>The handles <see cref="Release"/> was called with.</summary>
    public IReadOnlyList<LoginHandle> Released => _released;

    /// <summary>Gives <paramref name="target"/> a login in <paramref name="state"/>; an agent never given one is unsupported.</summary>
    /// <param name="target">The agent.</param>
    /// <param name="state">Ready, missing or expired.</param>
    /// <returns>This source.</returns>
    public FakeModelCredentialSource With(AgentId target, LoginState state)
    {
        _logins[target] = state;
        return this;
    }

    /// <inheritdoc />
    public bool Supports(AgentId target) => _logins.ContainsKey(target);

    /// <inheritdoc />
    public LoginStatus Probe(AgentId target) =>
        _logins.TryGetValue(target, out var state) ? new LoginStatus(state, Id, state == LoginState.Ready ? null : "fake") : new LoginStatus(LoginState.Missing, Id, "unsupported");

    /// <inheritdoc />
    public Task<Result<PreparedLogin>> Prepare(AgentId target, LaunchId launch, CancellationToken ct)
    {
        var status = Probe(target);
        if (!Supports(target) || status.State != LoginState.Ready)
        {
            var reason = !Supports(target) ? SandboxReasons.SourceUnsupported : status.State == LoginState.Expired ? SandboxReasons.Expired : SandboxReasons.NoLogin;
            return Task.FromResult<Result<PreparedLogin>>(SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, reason, source: Id));
        }

        var dir = $"/fake/{launch.Value}/{Id.ToString().ToLowerInvariant()}-{_prepared.Count}";
        var handle = new LoginHandle(dir);
        _prepared.Add(handle);
        var home = "/fake/home/." + target.Value;
        return Task.FromResult<Result<PreparedLogin>>(new PreparedLogin(
            Id,
            [new LoginMount(home, home, MountMode.ReadOnly), new LoginMount(dir, home + "-run", MountMode.ReadWrite)],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["CLAUDE_CONFIG_DIR"] = home + "-run" },
            [home + "-run"],
            handle));
    }

    /// <inheritdoc />
    public void Release(LoginHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _released.Add(handle);
    }
}
