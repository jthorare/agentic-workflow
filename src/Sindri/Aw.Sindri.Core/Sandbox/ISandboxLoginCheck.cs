namespace Aw.Sindri.Core.Sandbox;

/// <summary>The login probe of Check again (sandbox spec §3.4, §6): whether the chosen credential source can deliver each target's model login (-017).</summary>
public interface ISandboxLoginCheck
{
    /// <summary>Probes the login of every target the profile and the catalog name.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>One blocker per login that is missing, expired or unsupported; none when every login is ready.</returns>
    public ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken);
}
