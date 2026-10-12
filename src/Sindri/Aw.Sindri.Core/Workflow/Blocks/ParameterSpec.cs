namespace Aw.Sindri.Core.Workflow.Blocks;

/// <summary>A block's 'with:' parameter (spec §2).</summary>
/// <param name="Name">The parameter's name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Required">Whether a recipe must give it.</param>
/// <param name="IsCapabilityField">Whether it widens what the block may reach; then it takes only a literal or '{ profile }' (RCP-CAP-007).</param>
public sealed record ParameterSpec(string Name, ParameterType Type, bool Required, bool IsCapabilityField);
