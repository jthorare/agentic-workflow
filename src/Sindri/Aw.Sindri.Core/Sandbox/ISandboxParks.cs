namespace Aw.Sindri.Core.Sandbox;

/// <summary>The instances parked on a sandbox code, which Check again lists before it resumes any (sandbox spec §6, §8).</summary>
public interface ISandboxParks
{
    /// <summary>Lists the instances a park on SND-LAUNCH-014 to -017 holds.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parked instances, in any order.</returns>
    public ValueTask<IReadOnlyList<ParkedOnSandbox>> Parked(CancellationToken cancellationToken);
}
