namespace Aw.Sindri.Core.ModelHistory;

/// <summary>The model of an agent run: what the model-history fold remembers of a launch, and of a state's latest completed run.</summary>
/// <param name="Model">The model id.</param>
public sealed record RunModel(string Model);
