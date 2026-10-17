namespace Aw.Sindri.Core.Launches;

/// <summary>A model's price in USD per million tokens.</summary>
/// <param name="Prefix">The model id prefix it covers.</param>
/// <param name="Input">Fresh input.</param>
/// <param name="CacheRead">Cached input read.</param>
/// <param name="CacheWrite">Cached input written.</param>
/// <param name="Output">Output.</param>
public sealed record ModelPrice(string Prefix, decimal Input, decimal CacheRead, decimal CacheWrite, decimal Output);
