using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Testing;

/// <summary>What 'line doctor' finds in a checkout, set by the test: a checkout that has a test file, the evidence targets file and its credential variable, until a test says otherwise.</summary>
public sealed class FakeLineSetupProbe : ILineSetupProbe
{
    /// <summary>What the probe answers.</summary>
    public LineSetupFacts Facts { get; set; } = new(TestFiles: true, EvidenceConfig: true, CredentialSet: true);

    /// <summary>The variable the last read asked about.</summary>
    public string? LastVariable { get; private set; }

    /// <inheritdoc/>
    public ValueTask<LineSetupFacts> Read(string checkout, string? credentialVariable, CancellationToken cancellationToken)
    {
        LastVariable = credentialVariable;
        return ValueTask.FromResult(Facts with { CredentialSet = credentialVariable is null ? null : Facts.CredentialSet });
    }
}
