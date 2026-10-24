namespace Aw.Sindri.Core.Line;

/// <summary>What 'line doctor' finds in the checkout and the environment, which the profile alone does not say.</summary>
/// <param name="TestFiles">Whether the checkout holds a file the test-path convention names (<see cref="Checks.TestPaths"/>).</param>
/// <param name="EvidenceConfig">Whether the checkout has the evidence route's targets file (.agentic-workflow/evidence.json).</param>
/// <param name="CredentialSet">Whether the forge credential's environment variable is set; null when the credential is a command, which only runs per call.</param>
public sealed record LineSetupFacts(bool TestFiles, bool EvidenceConfig, bool? CredentialSet);
