namespace Aw.CodeIndex.Core.Model;

/// <summary>The graph layer's settings (spec §13).</summary>
/// <param name="Enabled">False leaves the layer disabled.</param>
/// <param name="MaxMegabytes">The largest graph.json read.</param>
public sealed record GraphSettings(bool Enabled, int MaxMegabytes);
