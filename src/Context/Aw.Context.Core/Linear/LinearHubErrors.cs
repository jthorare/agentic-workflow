using Aw.Kernel;

namespace Aw.Context.Core.Linear;

/// <summary>The Linear hub's errors that Context itself raises (spec 2026-11-04 §8). The rest of the SND-LINEAR codes belong to Sindri's write commands; this one comes from the write-through, so it is defined where it is raised.</summary>
public static class LinearHubErrors
{
    /// <summary>SND-LINEAR-013: Linear applied the change, but the answer was not stored (a purge of the linear source was running, or the re-read or the commit failed). Nothing is lost: the next sync stores it.</summary>
    public static readonly ErrorDefinition NotStored = new("SND-LINEAR-013", ErrorKind.Unavailable, "Saved in Linear; Sindri shows it after the next sync");
}
