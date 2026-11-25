namespace Aw.Sindri.Infrastructure.Workflow.Code;

/// <summary>What the compiler made of a body: the assembly, or the errors that stopped it.</summary>
/// <param name="Diagnostics">The errors; empty when <see cref="Assembly"/> is set.</param>
/// <param name="Assembly">The compiled assembly's bytes, or null when the body does not compile.</param>
internal sealed record CodeCompilation(IReadOnlyList<CodeDiagnostic> Diagnostics, byte[]? Assembly)
{
    /// <summary>True when the body compiled.</summary>
    public bool Succeeded => Assembly is not null;
}
