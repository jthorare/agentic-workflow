namespace Aw.Sindri.Core.Sandbox;

/// <summary>The session scopes alive now in 'sindri-steps.slice' (sandbox spec §3.3). The count is never stored: it is read here each time.</summary>
public interface ISliceScopes
{
    /// <summary>Lists the live scopes.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The scope unit names, for example 'sindri-LCH-….scope'. Empty when the slice does not exist yet.</returns>
    public Task<IReadOnlyList<string>> Live(CancellationToken cancellationToken);
}
