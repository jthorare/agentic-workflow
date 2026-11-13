namespace Aw.Sindri.Core.Setup;

/// <summary>How far a step is: <paramref name="Done"/> of <paramref name="Total"/>.</summary>
/// <param name="Done">Units finished.</param>
/// <param name="Total">Units in all.</param>
/// <param name="Unit">What is counted, plural ("files", "workflows", "repos").</param>
public sealed record SetupProgress(int Done, int Total, string Unit);
