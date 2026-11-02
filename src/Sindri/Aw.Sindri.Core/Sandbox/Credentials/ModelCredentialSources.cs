using Aw.Kernel;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>The registry of credential sources, keyed by id (sandbox spec SB18). Adapters are registered explicitly in the DI registration.</summary>
public sealed class ModelCredentialSources
{
    private readonly Dictionary<ModelLoginSource, IModelCredentialSource> _byId = [];
    private readonly TimeProvider _clock;

    /// <summary>Creates the registry.</summary>
    /// <param name="sources">The registered adapters; two with one id is a programming error.</param>
    /// <param name="clock">The clock the logins' expiry is read against.</param>
    public ModelCredentialSources(IEnumerable<IModelCredentialSource> sources, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        foreach (var source in sources)
        {
            if (!_byId.TryAdd(source.Id, source))
            {
                throw new ArgumentException($"Two credential sources are registered for '{source.Id}'.", nameof(sources));
            }
        }
    }

    /// <summary>Whether an adapter is registered for <paramref name="id"/>.</summary>
    /// <param name="id">The source id.</param>
    /// <returns>True when it is.</returns>
    public bool Has(ModelLoginSource id) => _byId.ContainsKey(id);

    /// <summary>Checks the login cheaply: ready for at least <paramref name="needed"/>, or SND-LAUNCH-017 with the source id. Never tries another source.</summary>
    /// <param name="id">The source.</param>
    /// <param name="target">The agent.</param>
    /// <param name="needed">How long the login must stay valid: the Step's wall time plus grace plus margin.</param>
    /// <returns>The status, or the -017 error.</returns>
    public Result<LoginStatus> Check(ModelLoginSource id, AgentId target, TimeSpan needed)
    {
        if (!_byId.TryGetValue(id, out var source) || !source.Supports(target))
        {
            return Unavailable(SandboxReasons.SourceUnsupported, id);
        }

        var status = source.Probe(target);
        if (status.State == LoginState.Ready)
        {
            return status.ExpiresAt is { } expires && expires - _clock.GetUtcNow() < needed
                ? Unavailable(SandboxReasons.Expired, id, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"the login runs out at {expires.UtcDateTime:yyyy-MM-dd HH:mm} UTC, before the {(int)needed.TotalMinutes} minutes this session needs"))
                : status;
        }

        return Unavailable(status.State == LoginState.Expired ? SandboxReasons.Expired : SandboxReasons.NoLogin, id, status.Detail);
    }

    /// <summary>Prepares the login for one launch from exactly the chosen source.</summary>
    /// <param name="id">The source.</param>
    /// <param name="target">The agent.</param>
    /// <param name="launch">The launch.</param>
    /// <param name="needed">How long the login must stay valid (see <see cref="Check(ModelLoginSource, AgentId, TimeSpan)"/>).</param>
    /// <param name="ct">Cancels the work.</param>
    /// <returns>The prepared login, or SND-LAUNCH-017 with the source id.</returns>
    public async Task<Result<PreparedLogin>> Prepare(ModelLoginSource id, AgentId target, LaunchId launch, TimeSpan needed, CancellationToken ct)
    {
        var status = Check(id, target, needed);
        return status.IsError ? status.Error : await _byId[id].Prepare(target, launch, ct).ConfigureAwait(false);
    }

    /// <summary>Releases a prepared login through its source.</summary>
    /// <param name="prepared">The prepared login.</param>
    public void Release(PreparedLogin prepared)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (_byId.TryGetValue(prepared.Source, out var source))
        {
            source.Release(prepared.Handle);
        }
    }

    private static Error Unavailable(string reason, ModelLoginSource id, string? detail = null) =>
        SandboxRefusal.For(SindriErrors.LaunchLoginUnavailable, reason, detail, id);
}
