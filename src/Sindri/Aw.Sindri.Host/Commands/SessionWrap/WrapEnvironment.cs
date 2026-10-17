using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The variables 'session wrap' sets before it starts the agent (launcher spec §3, L-12): the session is a Sindri child of this launch.</summary>
internal static class WrapEnvironment
{
    private static readonly string[] _passedOn = ["AW_SINDRI_ITEM", "AW_SINDRI_LAUNCH_SECRET", "AW_SINDRI_HOME"];

    /// <summary>The variables to set on top of the inherited environment.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="environment">Wrap's own environment.</param>
    /// <returns>AW_SINDRI_CHILD and AW_SINDRI_LAUNCH, and whichever of the item, the launch secret and the home wrap itself was given.</returns>
    internal static IReadOnlyDictionary<string, string> For(LaunchId launch, Func<string, string?> environment)
    {
        var set = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_CHILD"] = "1", ["AW_SINDRI_LAUNCH"] = launch.Value };
        foreach (var name in _passedOn)
        {
            if (environment(name) is { Length: > 0 } value)
            {
                set[name] = value;
            }
        }

        return set;
    }
}
