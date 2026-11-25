namespace Aw.Sindri.Features.BuildToolkit;

/// <summary>What a good build recorded.</summary>
/// <param name="Name">The toolkit name.</param>
/// <param name="SourceHash">The hash of the source that was built and tested.</param>
public sealed record BuildToolkitResult(string Name, string SourceHash);
