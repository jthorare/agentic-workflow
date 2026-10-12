namespace Aw.Sindri.Core.Ledger;

/// <summary>The ledger's migration state against this build.</summary>
/// <param name="Applied">Migrations applied to the file.</param>
/// <param name="Expected">Migrations this build ships.</param>
public sealed record SchemaStatus(int Applied, int Expected)
{
    /// <summary>Whether the file matches this build.</summary>
    public bool IsCurrent => Applied == Expected;
}
