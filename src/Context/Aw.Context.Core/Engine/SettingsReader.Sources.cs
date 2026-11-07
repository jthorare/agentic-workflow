namespace Aw.Context.Core.Engine;

public static partial class SettingsReader
{
    private static ContextSettings WithOwnedIndex(ContextSettings settings, CheckedSources sources, ContextIndexSettings index, ModelsSettings models) =>
        settings with
        {
            GitHub = sources.GitHub,
            Slack = sources.Slack,
            Library = sources.Library,
            LibraryModelRoots = LibraryRules.ModelRoots(sources.Library),
            Index = index,
            Models = models,
        };
}
