namespace Aw.Sindri.Core.Sandbox;

/// <summary>The form check of Check again (sandbox spec §6): whether each agent entry can be confined to a Step's policy (-015).</summary>
public interface ISandboxFormCheck
{
    /// <summary>Finds every entry that can't be confined.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>One blocker per entry that can't be confined; none when they all can.</returns>
    public ValueTask<IReadOnlyList<SandboxBlocker>> Check(CancellationToken cancellationToken);
}
