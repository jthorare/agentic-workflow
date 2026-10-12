namespace Aw.Sindri.Tests.Workflow.InvalidFixtures;

/// <summary>What an invalid fixture reports end to end.</summary>
/// <param name="Setup">What it is validated against.</param>
/// <param name="Own">Its own rule's line, at the position its rule-level test states.</param>
/// <param name="Others">Every other finding's line, in the service's order (none for a fixture with one fault).</param>
internal sealed record InvalidFixtureCase(FixtureSetup Setup, string Own, IReadOnlyList<string> Others);
