namespace Aw.Sindri.Features.GetSandboxStatus;

/// <summary>The confinement mechanism this platform runs (sandbox spec SB12), named without probing: 'bubblewrap', 'seatbelt' or 'none'.</summary>
internal interface ISandboxPlatformName
{
    /// <summary>Gets the platform's mechanism.</summary>
    public string Mechanism { get; }
}
