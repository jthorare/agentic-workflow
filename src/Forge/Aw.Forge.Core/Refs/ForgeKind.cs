namespace Aw.Forge.Core.Refs;

/// <summary>Which adapter a ref belongs to.</summary>
public enum ForgeKind
{
    /// <summary>GitHub (github.com or a GitHub Enterprise Server host).</summary>
    GitHub = 1,

    /// <summary>The file-backed forge that tests and proofs use.</summary>
    File = 2,
}
