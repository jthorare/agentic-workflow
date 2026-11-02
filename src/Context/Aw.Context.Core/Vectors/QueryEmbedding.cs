namespace Aw.Context.Core.Vectors;

/// <summary>A query's vector.</summary>
/// <param name="Model">The model name and digest that made it: it is comparable only with stored vectors of the same one.</param>
/// <param name="Vec">The components, L2-normalised.</param>
public sealed record QueryEmbedding(string Model, ReadOnlyMemory<float> Vec);
