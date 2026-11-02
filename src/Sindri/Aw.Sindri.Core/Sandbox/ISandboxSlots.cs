namespace Aw.Sindri.Core.Sandbox;

/// <summary>How many more sandboxed sessions may start now: the profile's cap less the live scopes (sandbox spec §3.3, §6).</summary>
public interface ISandboxSlots
{
    /// <summary>Counts the free slots.</summary>
    /// <param name="cancellationToken">Cancels the count.</param>
    /// <returns>The free slots; never negative.</returns>
    public ValueTask<int> Free(CancellationToken cancellationToken);
}
