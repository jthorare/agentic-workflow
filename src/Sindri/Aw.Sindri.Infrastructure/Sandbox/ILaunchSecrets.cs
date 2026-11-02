using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>The values of the variables a plan leaves to the launcher (the launch secret, a model key), per launch. They come in with the launch, never from the process environment.</summary>
internal interface ILaunchSecrets
{
    /// <summary>Gets the launch's values by variable name.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>The values; empty when the launch has none.</returns>
    public IReadOnlyDictionary<string, string> For(LaunchId launch);
}
