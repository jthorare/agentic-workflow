namespace Aw.Sindri.Core.Sandbox;

/// <summary>The rule for combining the model login source of the profile (or repo, or target) with a Step's ask (sandbox spec SB18, owner note 4): a Step only tightens.</summary>
public static class LoginSources
{
    /// <summary>The source a launch uses: the stricter of what the profile gives and what the Step asks for.</summary>
    /// <param name="configured">The source the profile, repo and target settle on.</param>
    /// <param name="ask">The Step's ask, or null for none.</param>
    /// <returns><paramref name="configured"/> when there is no ask or the ask is looser, else the ask.</returns>
    public static ModelLoginSource Stricter(ModelLoginSource configured, ModelLoginSource? ask) =>
        throw new NotSupportedException("the stricter-of rule is not built yet");
}
