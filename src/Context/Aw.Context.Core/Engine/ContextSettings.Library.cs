namespace Aw.Context.Core.Engine;

public sealed partial record ContextSettings
{
    /// <summary>The checked 'context.sources.library' entry, or null when the profile names none.</summary>
    public LibrarySettings? Library { get; init; }

    /// <summary>The library root names whose docs may reach a model call: 'aw' when 'stateDir' is on and 'models' is true, and each vault with 'models: true'.</summary>
    public IReadOnlyList<string> LibraryModelRoots { get; init; } = [];
}
