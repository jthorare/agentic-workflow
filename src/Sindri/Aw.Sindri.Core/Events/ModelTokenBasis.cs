namespace Aw.Sindri.Core.Events;

/// <summary>The values of <see cref="ContextModelCalled.TokenBasis"/>.</summary>
public static class ModelTokenBasis
{
    /// <summary>The CLI reported the counts.</summary>
    public const string Reported = "reported";

    /// <summary>Characters / 4, because the CLI gave none.</summary>
    public const string Estimated = "estimated";
}
