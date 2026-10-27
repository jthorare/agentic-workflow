using Aw.Context.Core.Graph;

namespace Aw.Context.Core.Glossary;

/// <summary>A feature's unit-length centroid.</summary>
internal sealed record Centroid(NodeKey Feature, double[] Unit);
