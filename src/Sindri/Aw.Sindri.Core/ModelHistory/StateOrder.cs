namespace Aw.Sindri.Core.ModelHistory;

/// <summary>The states an instance has entered, in the order each was first entered: what the model-history fold uses to tell a transition back from one forward.</summary>
/// <param name="Entered">The state names, oldest first.</param>
public sealed record StateOrder(IReadOnlyList<string> Entered);
