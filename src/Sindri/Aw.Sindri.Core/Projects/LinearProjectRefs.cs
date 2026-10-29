using Aw.Kernel;

namespace Aw.Sindri.Core.Projects;

/// <summary>Reads what the owner typed for a Linear project (projects page spec §5.3): a 'linear:project/&lt;id&gt;' key, or a Linear project url ('https://linear.app/&lt;workspace&gt;/project/&lt;slug&gt;-&lt;id&gt;').</summary>
public static class LinearProjectRefs
{
    /// <summary>Reads <paramref name="text"/> as a Linear project key or url.</summary>
    /// <param name="text">What the owner typed.</param>
    /// <returns>The key (its shape is checked by the decision that uses it) or the url's slug id, or SND-PROJECT-004 naming the 'linear' field.</returns>
    public static Result<LinearProjectRef> Parse(string text) => throw new NotSupportedException();
}
