namespace Aw.Context.Core.Engine;

/// <summary>What a cascade walk may do.</summary>
/// <param name="Hops">The hop kinds it may take.</param>
/// <param name="ComponentFanOut">The most features one code unit may implement before the component hop skips it (1 to 500).</param>
public sealed record CascadeOptions(CascadeHops Hops = CascadeHops.Default, int ComponentFanOut = CascadeOptions.DefaultFanOut)
{
    /// <summary>The component hop's default cap (profile key context.cascadeComponentFanOut).</summary>
    public const int DefaultFanOut = 50;

    /// <summary>The default walk: state, flow and dependency hops.</summary>
    public static CascadeOptions Default { get; } = new();
}
